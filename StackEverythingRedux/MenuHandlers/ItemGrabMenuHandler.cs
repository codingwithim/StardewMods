using System.Diagnostics;
using StackEverythingRedux.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using SFarmer = StardewValley.Farmer;

namespace StackEverythingRedux.MenuHandlers
{
    public class ItemGrabMenuHandler : BaseMenuHandler<ItemGrabMenu>
    {
        /// <summary>Native player inventory menu.</summary>
        private InventoryMenu PlayerInventoryMenu = null;

        /// <summary>Native shop inventory menu.</summary>
        private InventoryMenu ItemsToGrabMenu = null;

        /// <summary>If the callbacks have been hooked yet so we don't do it unnecessarily.</summary>
        private bool CallbacksHooked = false;

        /// <summary>Keeps a reference to the menu instance where callbacks were hooked.</summary>
        private ItemGrabMenu HookedMenu = null;

        /// <summary>Native item select callback.</summary>
        private ItemGrabMenu.behaviorOnItemSelect OriginalItemSelectCallback;

        /// <summary>Native item grab callback.</summary>
        private ItemGrabMenu.behaviorOnItemSelect OriginalItemGrabCallback;

        /// <summary>The item being hovered when the split menu is opened.</summary>
        private Item HoverItem = null;

        /// <summary>The amount we wish to buy/sell.</summary>
        private int StackAmount = 0;

        /// <summary>The total number of items in the hovered stack.</summary>
        private int TotalItems = 0;

        /// <summary>Convenience: the currently held item (in the Native Menu).</summary>
        private Item HeldItem => NativeMenu?.heldItem;

        public ItemGrabMenuHandler()
            : base()
        {
            // We're handling the inventory in such a way that we don't need the generic handler.
            HasInventory = false;

            // Auto-unhook if the active menu changes while split is open or hooks are active.
            StackEverythingRedux.Instance.Helper.Events.Display.MenuChanged += OnMenuChanged;
        }

        /// <summary>Allows derived handlers to provide additional checks before opening the split menu.</summary>
        protected override bool CanOpenSplitMenu()
        {
            bool canOpen = NativeMenu?.allowRightClick == true;
            return canOpen && base.CanOpenSplitMenu();
        }

        /// <summary>Tells the handler to close the split menu.</summary>
        public override void CloseSplitMenu()
        {
            // Always cleanup callbacks even if base already closed the split.
            RestoreNativeCallbacks();

            base.CloseSplitMenu();
        }

        /// <summary>Called when the current handler loses focus when the split menu is open, allowing it to cancel the operation or run the default behaviour.</summary>
        protected override EInputHandled CancelMove()
        {
            // Not hovering above anything so pass-through
            if (HoverItem is null)
            {
                return EInputHandled.NotHandled;
            }

            // Ensure callbacks are active so we keep control of the split amount.
            _ = HookCallbacks();

            // Run the regular command
            NativeMenu?.receiveRightClick(ClickItemLocation.X, ClickItemLocation.Y);

            CloseSplitMenu();

            // Consume input so the menu doesn't run left click logic as well
            return EInputHandled.Consumed;
        }

        /// <summary>Main event that derived handlers use to setup necessary hooks and other things needed to take over how the stack is split.</summary>
        protected override EInputHandled OpenSplitMenu()
        {
            try
            {
                PlayerInventoryMenu = NativeMenu.inventory;
                ItemsToGrabMenu = NativeMenu.ItemsToGrabMenu;

                // Emulate the right click method that would normally happen (??)
                HoverItem = NativeMenu.hoveredItem;
            }
            catch (Exception e)
            {
                Log.Error($"[{nameof(ItemGrabMenuHandler)}.{nameof(OpenSplitMenu)}] Had an exception:\n{e}");
                return EInputHandled.NotHandled;
            }

            // Do nothing if we're not hovering over an item, or item is single (no point in splitting)
            if (HoverItem == null || HoverItem.Stack <= 1)
            {
                return EInputHandled.NotHandled;
            }

            TotalItems = HoverItem.Stack;
            // +1 before /2 ensures number is rounded UP
            StackAmount = (TotalItems + 1) / 2; // default at half

            // Create the split menu
            SplitMenu = new StackSplitMenu(OnStackAmountReceived, StackAmount);
            return EInputHandled.Consumed;
        }

        /// <summary>Callback given to the split menu that is invoked when a value is submitted.</summary>
        protected override void OnStackAmountReceived(string s)
        {
            // Store amount
            if (int.TryParse(s, out StackAmount))
            {
                if (StackAmount > 0)
                {
                    if (!HookCallbacks())
                    {
                        // failed to hook, bail out cleanly
                        base.OnStackAmountReceived(s);
                        return;
                    }

                    try
                    {
                        // Drive the vanilla right-click path so the menu sets heldItem etc.
                        NativeMenu?.receiveRightClick(ClickItemLocation.X, ClickItemLocation.Y);
                    }
                    finally
                    {
                        // Ensure we never leave callbacks dangling in case of exceptions or external menu swaps.
                        RestoreNativeCallbacks();
                    }
                }
                else
                {
                    RevertItems();
                }
            }

            base.OnStackAmountReceived(s);
        }

        /// <summary>Callback override for when an item in the inventory is selected.</summary>
        private void OnItemSelect(Item item, SFarmer who)
        {
            MoveItems(item, who, PlayerInventoryMenu, OriginalItemSelectCallback);
        }

        /// <summary>Callback override for when an item in the shop is selected.</summary>
        private void OnItemGrab(Item item, SFarmer who)
        {
            MoveItems(item, who, ItemsToGrabMenu, OriginalItemGrabCallback);
        }

        /// <summary>Updates the number of items being held by the player based on what was input to the split menu.</summary>
        private void MoveItems(Item item, SFarmer who, InventoryMenu inventoryMenu, ItemGrabMenu.behaviorOnItemSelect callback)
        {
            Debug.Assert(StackAmount > 0);

            // Get the held item now that it's been set by the native receiveRightClick call
            Item heldItem = HeldItem;
            if (heldItem != null)
            {
                int wantToHold = Math.Min(TotalItems, StackAmount);

                HoverItem.Stack = TotalItems - wantToHold;
                heldItem.Stack = wantToHold;

                item.Stack = wantToHold;

                // Remove the empty item from the inventory
                if (HoverItem.Stack <= 0 && inventoryMenu != null)
                {
                    int index = inventoryMenu.actualInventory.IndexOf(HoverItem);
                    if (index > -1)
                    {
                        inventoryMenu.actualInventory[index] = null;
                    }
                }
            }

            // restore before invoking the original callback to avoid keeping hooks if callback changes menus
            RestoreNativeCallbacks();

            // Continue vanilla flow
            callback?.Invoke(item, who);
        }

        /// <summary>Cancels the operation so no items are sold or bought.</summary>
        private void RevertItems()
        {
            if (HoverItem != null && TotalItems > 0)
            {
                Log.Trace($"[{nameof(ItemGrabMenuHandler)}.{nameof(RevertItems)}] Reverting items");
                HoverItem.Stack = TotalItems;

                RestoreNativeCallbacks();
            }
        }

        /// <summary>Replaces the native shop callbacks with our own so we can intercept the operation to modify the amount.</summary>
        private bool HookCallbacks()
        {
            // If already hooked for this exact menu, do nothing.
            if (CallbacksHooked && ReferenceEquals(HookedMenu, NativeMenu))
            {
                return true;
            }

            try
            {
                IReflectedField<ItemGrabMenu.behaviorOnItemSelect> itemSelectCallbackField = StackEverythingRedux.Reflection
                    .GetField<ItemGrabMenu.behaviorOnItemSelect>(NativeMenu, "behaviorFunction");

                OriginalItemGrabCallback = NativeMenu.behaviorOnItemGrab;
                OriginalItemSelectCallback = itemSelectCallbackField.GetValue();

                NativeMenu.behaviorOnItemGrab = new ItemGrabMenu.behaviorOnItemSelect(OnItemGrab);
                itemSelectCallbackField.SetValue(new ItemGrabMenu.behaviorOnItemSelect(OnItemSelect));

                HookedMenu = NativeMenu;
                CallbacksHooked = true;
                return true;
            }
            catch (Exception e)
            {
                Log.Error($"[{nameof(ItemGrabMenuHandler)}.{nameof(HookCallbacks)}] Failed to hook ItemGrabMenu callbacks:\n{e}");
                return false;
            }
        }

        /// <summary>Sets the callbacks back to the native ones (idempotent).</summary>
        private void RestoreNativeCallbacks()
        {
            if (!CallbacksHooked)
            {
                return;
            }

            try
            {
                ItemGrabMenu menu = HookedMenu ?? NativeMenu;
                if (menu != null)
                {
                    IReflectedField<ItemGrabMenu.behaviorOnItemSelect> itemSelectCallbackField = StackEverythingRedux.Reflection
                        .GetField<ItemGrabMenu.behaviorOnItemSelect>(menu, "behaviorFunction");

                    itemSelectCallbackField.SetValue(OriginalItemSelectCallback);
                    menu.behaviorOnItemGrab = OriginalItemGrabCallback;
                }
            }
            catch (Exception e)
            {
                Log.Error($"[{nameof(ItemGrabMenuHandler)}.{nameof(RestoreNativeCallbacks)}] Failed to restore native callbacks:\n{e}");
            }
            finally
            {
                CallbacksHooked = false;
                HookedMenu = null;
                OriginalItemSelectCallback = null;
                OriginalItemGrabCallback = null;
            }
        }

        /// <summary>Auto-cleanup when another mod replaces/closes the chest menu.</summary>
        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            // If hooks are active and the hooked menu is no longer the current one, unhook safely.
            if (CallbacksHooked && !ReferenceEquals(e.NewMenu, HookedMenu))
            {
                RestoreNativeCallbacks();
            }
        }
    }
}
