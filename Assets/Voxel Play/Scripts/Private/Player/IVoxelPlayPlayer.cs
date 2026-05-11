using System.Collections.Generic;
using UnityEngine;

namespace VoxelPlay {
    public partial interface IVoxelPlayPlayer {
        event OnPlayerInventoryEvent OnItemSelectedChanged;
        event OnPlayerGetDamageEvent OnPlayerGetDamage;
        event OnPlayerIsKilledEvent OnPlayerIsKilled;
        event OnPlayerInventoryItemQuantityChange OnItemAdded;
        event OnPlayerInventoryItemQuantityChange OnItemConsumed;
        event OnPlayerInventoryClear OnItemsClear;

        // Inventory related
        void AddInventoryItem (ItemDefinition[] newItems);
        bool AddInventoryItem (ItemDefinition newItem, float quantity = 1);
        void PickUpItem (ItemDefinition newItem, float quantity = 1);
        void UnSelectItem ();
        bool SetSelectedItem (int itemIndex);
        bool SetSelectedItem (InventoryItem item);
        bool SetSelectedItem (VoxelDefinition v);
        InventoryItem GetSelectedItem ();
        List<InventoryItem> items { get; }
        int selectedItemIndex { get; set; }
        List<InventoryItem> GetPlayerItems ();
        bool HasItem (ItemDefinition item);
        InventoryItem ConsumeItem ();
        void ConsumeItem (ItemDefinition item);
        void ConsumeItem (ItemDefinition item, float amount);
        void ConsumeAllItems ();
        float GetItemQuantity (ItemDefinition item);
        InventoryItem GetInventoryItem (VoxelDefinition voxelDefinition);

        // Combat related
        void DamageToPlayer (int damagePoints);
        float GetHitDelay ();
        float GetHitRange ();
        int GetHitDamage ();
        int GetHitDamageRadius ();

        // General character stats
        string playerName { get; set; }
        float hitDelay { get; set; }
        int hitDamage { get; set; }
        float hitRange { get; set; }
        int hitDamageRadius { get; set; }

        // Other
        Transform GetTransform ();
        Color selectedItemTintColor { get; set; }
    }
}
