using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BenScr.MinecraftClone
{
    public class InventoryManager : MonoBehaviour
    {
        [SerializeField] private Transform barSlotLayout, backpackSlotLayout;
        [SerializeField] private GameObject backPackScreen;
        public GameObject slotItemPrefab;

        public static List<Slot> slotDatas;
        public static Slot[] barSlots;
        public static int barSlotsCount => barSlots.Length;
        public static int playerSlotsCount;

        private Slot selectedSlot => barSlots[currentSlotIndex];

        public Image selectedBarSlotImage;

        public static Item selectedItem = null;

        public static Action<Slot> OnSwitchSlot;
        public static Action<Slot> OnUpdateSlot;

        private int currentSlotIndex;
        public const int MAX_DURATION_VALUE = -1;

        public static InventoryManager instance;
        public DynamicObjectPool<GameObject> pool = new DynamicObjectPool<GameObject>();

        public bool addItem;
        public PrizeData prize;

        private void Awake()
        {
            instance = this;
            selectedItem = null;
            InitSlots(); 
        }

        private void Start()
        {
            UpdateSlot();
            SwitchedSlot();
        }

        private void InitSlots()
        {
            int barSlotsCount = barSlotLayout.childCount;
            int backpackSlotsCount = backpackSlotLayout.childCount;
            playerSlotsCount = barSlotsCount + backpackSlotsCount;
            barSlots = new Slot[barSlotsCount];

            slotDatas = new List<Slot>(playerSlotsCount);

            int i = 0;
            foreach (Transform tr in barSlotLayout)
            {
                slotDatas.Add(new Slot(tr));
                barSlots[i] = slotDatas[i];
                i++;
            }
            foreach (Transform tr in backpackSlotLayout)
            {
                slotDatas.Add(new Slot(tr));
                i++;
            }
        }

        public static void AddToTargetSlots(Item itemToAdd, TargetSlotArea targetSlotArea)
        {
            int start, end;
            if (targetSlotArea == TargetSlotArea.Backpack) { start = barSlotsCount; end = playerSlotsCount; }
            else if (targetSlotArea == TargetSlotArea.Chest) { start = playerSlotsCount; end = slotDatas.Count; }
            else { start = 0; end = barSlotsCount; }

            for (int i = start; i < end; i++)
            {
                if (itemToAdd.amount <= 0) return;

                Item item = slotDatas[i].item;

                if (item != null && item.Matches(itemToAdd))
                {
                    DragAndDropSystem.AddToItem(itemToAdd, item, itemToAdd.amount);
                }
                else if (item == null)
                {
                    DuplicateItem(itemToAdd, slotDatas[i], itemToAdd.amount);
                    DragAndDropSystem.DestroyDragging();
                    return;
                }
            }
        }

        public static void CreateNewItem(ItemData item, int amount, int duration, Slot slot)
        {
            slot.item = new Item(amount, duration, instance.pool.Get(instance.slotItemPrefab, instance.slotItemPrefab, slot.transform).transform, item);
        }

        public static void DuplicateItem(Item item, Slot slotData, int amount)
        {
            slotData.item = new Item(amount, item.duration, instance.pool.Get(instance.slotItemPrefab, instance.slotItemPrefab, slotData.transform).transform, item.itemData);
        }

        public static void AddItem(ItemData item, int amount, int duration = MAX_DURATION_VALUE, bool checkForDrop = true)
        {
            if (amount == 0) return;
            duration = (duration == MAX_DURATION_VALUE) ? item.maxDuration : duration;

            amount = AddItemInternal(item, amount, duration);

            if (amount > 0 && checkForDrop)
            {
                //DropItemManager.DropItem(item, amount, item.maxDuration, PlayerController.GetRandomizedForwardPos());
            }

            instance.UpdateSlot();
        }

        public static int AddItemFromOther(Item item, int amount, int duration)
        {
            amount = AddItemInternal(item.itemData, amount, duration);
            instance.UpdateSlot();
            return amount;
        }

        public void UpdateSlot()
        {
            OnUpdateSlot?.Invoke(selectedSlot);
        }

        private static int AddItemInternal(ItemData item, int amount, int duration)
        {
            for (int i = 0; i < playerSlotsCount && amount > 0; i++)
            {
                var p = slotDatas[i].item;
                if (p != null && p.Matches(item, duration) && p.amount < item.stackSize)
                {
                    amount = AddAmountToItemData(p, amount);
                }
            }

            for (int i = 0; i < playerSlotsCount && amount > 0; i++)
            {
                var s = slotDatas[i];
                if (s.item == null)
                {
                    int space = item.stackSize;
                    int add = amount < space ? amount : space;
                    amount -= add;
                    CreateNewItem(item, add, duration, s);
                }
            }

            return amount;
        }

        public static void RemoveItem(PrizeData prizeData)
        {
            int length = prizeData.prizeItems.Length;
            for (int i = 0; i < length; i++)
            {
                RemoveItem(prizeData.prizeItems[i], prizeData.prizeAmounts[i]);
            }
        }

        public static void RemoveItem(ItemData item, int amount)
        {
            if (amount <= 0) return;

            for (int i = 0; i < playerSlotsCount && amount > 0; i++)
            {
                var p = slotDatas[i].item;
                if (p != null && p.Matches(item))
                {
                    if (RemoveAmountFromItem(p, ref amount))
                      DragAndDropSystem.DestroyItem(slotDatas[i]);
                }
            }

            instance.UpdateSlot();
        }

        public static int RemoveItem(Slot slot, int amount)
        {
            if (amount <= 0) return amount;

            var p = slot.item;
            if (p == null) return amount;

            if (RemoveAmountFromItem(p, ref amount))
                DragAndDropSystem.DestroyItem(slot);

            instance.UpdateSlot();
            return amount;
        }

        private static bool RemoveAmountFromItem(Item item, ref int amount)
        {
            int toRemove = amount < item.amount ? amount : item.amount;
            item.amount -= toRemove;
            amount -= toRemove;

            if (item.amount > 0) item.Update();
            return item.amount <= 0;
        }

        public static int AddAmountToItemData(Item itemData, int amount)
        {
            int space = itemData.itemData.stackSize - itemData.amount;
            int add = amount < space ? amount : space;
            itemData.amount += add;
            itemData.Update();
            return amount - add;
        }
        private void Update()
        {
            if(Input.mouseScrollDelta.y >= 1.0)
            {
                currentSlotIndex = (currentSlotIndex - 1 + barSlotsCount) % barSlotsCount;
                SwitchedSlot();
            }
            else if (Input.mouseScrollDelta.y <= -1.0)
            {
                currentSlotIndex = (currentSlotIndex + 1) % barSlotsCount;
                SwitchedSlot();
            }

            if (addItem)
            {
                for(int i = 0; i < prize.prizeAmounts.Length; i++)
                {
                    AddItem(prize.prizeItems[i], prize.prizeAmounts[i]);
                }

                addItem = false;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                if(CanvasScreenManager.activeScreen != backPackScreen)
                CanvasScreenManager.instance.OpenScreen(backPackScreen);
                else
                    CanvasScreenManager.instance.CloseActiveScreen();
            }
        }

        private void SwitchedSlot()
        {
            selectedBarSlotImage.transform.position = barSlots[currentSlotIndex].transform.position;
            selectedItem = selectedSlot.item;
            OnSwitchSlot?.Invoke(selectedSlot);
        }

        public static Slot FindEmptySlotForItemReturn()
        {
            for (int i = 0; i < slotDatas.Count; i++)
            {
                var s = slotDatas[i];
                if (s.item == null && s.transform.gameObject.activeInHierarchy) return s;
            }
            return null;
        }
        public static Slot FindEmpySlot()
        {
            for (int i = 0; i < playerSlotsCount; i++)
                if (slotDatas[i].item == null)
                    return slotDatas[i];
            return null;
        }
    }

    [System.Flags]
    public enum SlotType
    {
        None = 0,
        Weapon = 1 << 0,
        Armor = 1 << 1,
        Potion = 1 << 2,
        QuestItem = 1 << 3,
        Crafting = 1 << 4,
        All = ~0
    }

    public enum TargetSlotArea { PlayerBar, Backpack, Chest }

    public class Slot
    {
        public Transform transform;
        public Item item;
        public SlotType type;

        public Slot(Transform tr)
        {
            transform = tr;
            type = SlotType.All;
        }
        public Slot(Transform transform, SlotType slotType)
        {
            this.transform = transform;
            this.type = slotType;
        }
    }

    [Serializable]
    public class Item
    {
        public int amount, duration;
        public Transform transform;
        public readonly ItemData itemData;

        public bool hasMaxAmount => amount == itemData.stackSize;

        private TextMeshProUGUI amountTxt;

        public bool Matches(Item other) => other.itemData == itemData && other.duration == duration;
        public bool Matches(Item other, int otherDuration) => other.itemData == itemData && (otherDuration == InventoryManager.MAX_DURATION_VALUE ? other.duration : otherDuration) == duration;
        public bool Matches(ItemData other) => other == itemData && other.maxDuration == duration;

        public bool Matches(ItemData other, int otherDuration) => other == itemData && (otherDuration == InventoryManager.MAX_DURATION_VALUE ? other.maxDuration : otherDuration) == duration;

        public Item(int amount, int duration, Transform transform, ItemData itemData)
        {
            this.amount = amount;
            this.duration = duration;
            this.transform = transform;
            this.itemData = itemData;

            transform.GetComponent<Image>().sprite = itemData.sprite;
            amountTxt = transform.GetChild(0).GetComponent<TextMeshProUGUI>();
            amountTxt.text = amount.ToString();
        }

        public void Update()
        {
            amountTxt.text = amount.ToString();
        }
    }
}