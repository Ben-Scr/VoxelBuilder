
namespace BenScr.MinecraftClone
{
    using System.Linq;
    using TMPro;
    using Unity.Mathematics;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.InputSystem;
    using UnityEngine.UI;

    public class DragAndDropSystem : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private GraphicRaycaster graphicRaycaster;

        [SerializeField] private Transform _canvas;

        [SerializeField] private InputActionReference combineReference;
        [SerializeField] private InputActionReference autoMoveReference;

        [SerializeField] private TextMeshProUGUI pointedItemInfo;
        [SerializeField] private GameObject itemInteractionInfo;

        public static Transform canvas => instance._canvas;

        public static DraggedItem draggingItem;

        private DraggedItem lastDraggingItem;

        private float dragStartTime = 0;

        public static DragAndDropSystem instance;

        private Slot hoveringSlotData;
        private Slot lastHorveredSlotData;
        public static Transform pointedTransform;

        [Header("Slot Hover Animation")]
        private float progress = 0;
        [SerializeField] private float hoverAnimSpeed;
        [SerializeField] private Color hoverColor;
        private Color originalColor;

        private void Awake()
        {
            instance = this;
            draggingItem = null;

        }

        private void OnEnable()
        {
            combineReference.action.Enable();
            autoMoveReference.action.Enable();
        }
        private void OnDisable()
        {

            combineReference.action.Disable();
            autoMoveReference.action.Disable();
        }

        public static bool IsSlotCompatible(Slot slot, ItemType itemType)
        {
            if (slot.type == SlotType.All || slot.type == SlotType.Crafting)
                return true;
            if (slot.type == SlotType.None)
                return false;

            return (slot.type & (SlotType)itemType) != 0;
        }

        public void OnPointerDown(PointerEventData eventData) // Used for Selecting a SlotItem
        {
            if (eventData.pointerEnter == null || hoveringSlotData == null || hoveringSlotData.item == null || draggingItem != null)
            {
                return;
            }

            if (!Input.GetMouseButtonDown(1))
            {
                SelectItem(new DraggedItem(hoveringSlotData, hoveringSlotData.item));
                hoveringSlotData.item = null;

                CheckBindings();
            }
            else
            {
                int halfAmount = (int)math.ceil(hoveringSlotData.item.amount / 2f);
                int difference = hoveringSlotData.item.amount - halfAmount;

                hoveringSlotData.item.amount = halfAmount;
                hoveringSlotData.item.Update();
                SelectItem(new DraggedItem(hoveringSlotData, hoveringSlotData.item));

                if (difference > 0)
                {
                   InventoryManager. DuplicateItem(hoveringSlotData.item, hoveringSlotData, difference);
                }

                else
                {
                    hoveringSlotData.item = null;
                }

                CheckBindings();
            }

            /*if (hoveringSlotData.type == SlotType.Crafting)
            {
                ItemCraftingManager.instance.UpdateCraftingResult();
            }
            else if (hoveringSlotData.transform == ItemCraftingManager.instance.resultSlotTr)
            {
                ItemCraftingManager.instance.Craft();
                ItemCraftingManager.instance.UpdateCraftingResult();
            }*/
        }

        private void ShowItemPointerInfo()
        {
            if (hoveringSlotData?.item != null)
            {
                pointedItemInfo.text = hoveringSlotData.item.itemData.Name;
                itemInteractionInfo.transform.position = hoveringSlotData.transform.position + new Vector3(50, 50, 0);
                itemInteractionInfo.SetActive(true);
            }
            else
            {
                itemInteractionInfo.SetActive(false);
            }
        }

        private void CheckBindings()
        {
            if (combineReference.action.IsPressed())
            {
                CombineItems();
            }
            else if (autoMoveReference.action.IsPressed())
            {
                //bool chestOpen = ChestHandler.selectedChestInfo != null && !ChestHandler.selectedChestInfo.chestData.luckChest;
                bool chestOpen = false;
                TargetSlotArea targetSlotArea = chestOpen ? TargetSlotArea.Chest : (InventoryManager.barSlots.Contains(hoveringSlotData) ? TargetSlotArea.Backpack : TargetSlotArea.PlayerBar);
                InventoryManager.AddToTargetSlots(draggingItem.item, targetSlotArea);
            }
        }

        public static int IndexOf<T>(in T item, in T[] array)
        {
            int i = 0;
            foreach (var element in array)
            {
                if (element.Equals(item))
                {
                    return i;
                }
                i++;
            }

            throw new System.Exception("Item not found");
        }

        public void CombineItems()
        {
            if (draggingItem.item.hasMaxAmount) return;

            // bool destroyWhenEmptyChestSlot = (ChestHandler.selectedChestInfo?.chestData.destroyWhenEmpty ?? false) && (IndexOf(draggingItem.slotData, InventoryManager.slotDatas.ToArray()) >= InventoryManager.playerSlotsCount);
            bool destroyWhenEmptyChestSlot = false;
            int start = destroyWhenEmptyChestSlot ? InventoryManager.playerSlotsCount : 0;
            int end = (destroyWhenEmptyChestSlot || /*GameDataRegistry.GameData.settings.Combine_Items_From_Chest*/ true) ? InventoryManager.slotDatas.Count : InventoryManager.playerSlotsCount;

            for (int i = start; i < end; i++)
            {
                if (draggingItem.item.hasMaxAmount) return;

                var slotData = InventoryManager.slotDatas[i];

                if (slotData.item == null) continue;

                bool itemsFit = slotData.item.Matches(draggingItem.item);
                if (!itemsFit) continue;

                //bool canAddItem = GameDataRegistry.GameData.settings.Comine_Items_With_MaxStackSize || slotData.persistentItem.amount < slotData.persistentItem.itemData.stackSize;
                bool canAddItem = true;

                if (canAddItem)
                {
                    AddToItem(slotData, draggingItem.item, slotData.item.amount);
                }
            }
        }

        public static void SelectItem(DraggedItem draggingItem)
        {
            DragAndDropSystem.draggingItem = draggingItem;
            DragAndDropSystem.draggingItem.item.transform.SetParent(canvas);
            instance.dragStartTime = Time.realtimeSinceStartup;
        }

        public void Update()
        {
            pointedTransform = GetHoveredTransform(graphicRaycaster);
            hoveringSlotData = GetSlotDataByTransform(pointedTransform);

            SlotAnimation();
            ShowItemPointerInfo();

            if (draggingItem == null) return;

            DraggingItemLogic();
        }

        public void DraggingItemLogic()
        {
            draggingItem.item.transform.position = Input.mousePosition;

            if (lastDraggingItem != draggingItem)
                draggingItem.item.transform.GetComponent<RectTransform>().sizeDelta = new Vector2(60, 60);

            lastDraggingItem = draggingItem;

            if (Time.realtimeSinceStartup - dragStartTime < 0.1f) return;

            bool leftMouse = Input.GetMouseButtonDown(0), rightMouse = Input.GetMouseButtonDown(1);
            int dropAmount = leftMouse ? draggingItem.item.amount : (rightMouse ? 1 : 0);

            if (dropAmount == 0)
            {
                return;
            }

            if (pointedTransform == null)
            {
                DropItem(dropAmount);
                return;
            }

            if (hoveringSlotData == null || !IsSlotCompatible(hoveringSlotData, draggingItem.item.itemData.type))
            {
                if (pointedTransform.name == "Drop_Layer")
                    DropItem(dropAmount);
                else
                    ReturnItem();

                return;
            }

            if (leftMouse)
            {
                if (hoveringSlotData.item == null)
                    SetSlotItem(hoveringSlotData);

                else if (!hoveringSlotData.item.Matches(draggingItem.item))
                    SwitchSlotItems(hoveringSlotData);
                else
                    AddToItem(draggingItem.item, hoveringSlotData, dropAmount);
            }
            else
            {
                if (hoveringSlotData.item == null)
                {
                   InventoryManager.DuplicateItem(draggingItem.item, hoveringSlotData, 1);
                    draggingItem.item.amount--;

                    if (draggingItem.item.amount == 0)
                        DestroyDragging();

                    else
                        draggingItem.item.Update();
                }
                else if (hoveringSlotData.item.Matches(draggingItem.item))
                {
                    AddToItem(draggingItem.item, hoveringSlotData, 1);
                }
            }

            if (hoveringSlotData.type == SlotType.Crafting)
            {
                //ItemCraftingManager.instance.UpdateCraftingResult();
            }
        }

        private void SlotAnimation()
        {
            if (lastHorveredSlotData != hoveringSlotData)
            {
                if (lastHorveredSlotData?.transform != null)
                {
                    lastHorveredSlotData.transform.GetComponent<Image>().color = originalColor;
                }

                progress = 0;
            }

            if (hoveringSlotData != null)
            {
                lastHorveredSlotData = hoveringSlotData;

                if (progress <= 1)
                {
                    AnimateSlotColor();
                }
            }
        }
        private void AnimateSlotColor()
        {
            var img = lastHorveredSlotData.transform.GetComponent<Image>();

            if (progress == 0)
            {
                originalColor = img.color;
            }

            img.color = Color.Lerp(originalColor, hoverColor, progress);
            progress += Time.deltaTime * hoverAnimSpeed;
        }

        public static void AddToItem(Item from, Slot toSlot, int amount)
        {
            Item to = toSlot.item;

            if (to.amount == to.itemData.stackSize)
            {
                SwitchSlotItems(toSlot);
                return;
            }

            int difference = to.amount + amount - from.itemData.stackSize; // 5 + 10 - 64 = -49
            int amountToAdd = amount - math.clamp(difference, 0, from.amount);

            to.amount += amountToAdd;

            to.Update();

            from.amount -= amountToAdd;

            if (from.amount <= 0)
                DestroyDragging();

            else
                from.Update();
        }

        public static void AddToItem(Item from, Item to, int amount)
        {
            int difference = (to.amount + amount) - from.itemData.stackSize; // 5 + 10 - 64 = -49
            int amountToAdd = amount - math.clamp(difference, 0, from.amount);

            to.amount += amountToAdd;

            to.Update();

            from.amount -= amountToAdd;

            if (from.amount <= 0)
                DestroyDragging();

            else
                from.Update();
        }

        public static void AddToItem(Slot fromSlot, Item to, int amount)
        {
            int difference = (to.amount + amount) - fromSlot.item.itemData.stackSize; // 5 + 10 - 64 = -49
            int amountToAdd = amount - math.clamp(difference, 0, fromSlot.item.amount);

            to.amount += amountToAdd;

            to.Update();

            fromSlot.item.amount -= amountToAdd;

            if (fromSlot.item.amount <= 0)
                DestroyItem(fromSlot);

            else
                fromSlot.item.Update();
        }

        public static void DestroyDragging()
        {
            InventoryManager.instance.pool.Release(InventoryManager.instance.slotItemPrefab, draggingItem.item.transform.gameObject);
            draggingItem.item = null;
            draggingItem = null;
        }

        public static void DestroyItem(Slot slotData)
        {
            if (slotData.item == null) return;

            InventoryManager.instance.pool.Release(InventoryManager.instance.slotItemPrefab, slotData.item.transform.gameObject);
            slotData.item = null;
        }

        public static void SwitchSlotItems(Slot slotData)
        {
            Item draggedItem = draggingItem.item;
            Item switchItem = slotData.item;

            SelectItem(new DraggedItem(draggingItem.slotData, switchItem));

            draggedItem.transform.SetParent(slotData.transform);
            slotData.item = draggedItem;

            draggingItem.item.transform.position = Input.mousePosition;
        }

        public void SetSlotItem(Slot slotData)
        {
            slotData.item = draggingItem.item;
            slotData.item.transform.SetParent(slotData.transform);

            DeselectDragging();
        }

        public void DeselectDragging()
        {
            draggingItem = null;
        }

        public void ReturnItem() //Returns the Dragging Item
        {
            if (draggingItem.slotData.item == null)
            {
                draggingItem.slotData.item = draggingItem.item;
                draggingItem.item.transform.SetParent(draggingItem.slotData.transform);
                draggingItem = null;
            }
            else if (draggingItem.slotData.item.itemData == draggingItem.item.itemData)
            {
                AddToItem(draggingItem.item, draggingItem.slotData, draggingItem.item.amount);
            }
            else
            {
                Slot slotData = InventoryManager.FindEmptySlotForItemReturn();

                if (slotData != null)
                {
                    slotData.item = draggingItem.item;
                    draggingItem.item.transform.SetParent(slotData.transform);
                    draggingItem = null;
                }
                else
                {
                    DropItem(draggingItem.item.amount);
                }
            }
        }

        public void DropItem(int amount)
        {
            //DropItemManager.DropItem(draggingItem.item, amount, RainixUtitlity.GetWorldMousePosition2D(), false);

            if (draggingItem.item.amount <= 0)
                DestroyDragging();
        }

        public static Transform GetHoveredTransform(GraphicRaycaster graphicRaycaster)
        {
            PointerEventData eventData = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
            var results = new System.Collections.Generic.List<RaycastResult>();
            graphicRaycaster.Raycast(eventData, results);
            if (results.Count > 0) return results[0].gameObject.transform;
            return null;
        }

        public static Slot GetSlotDataByTransform(in Transform transform)
        {
            foreach (var slotData in InventoryManager.slotDatas)
            {
                if (slotData.transform == transform) return slotData;
            }

            return null;
        }
    }

    public class DraggedItem
    {
        public Slot slotData;
        public Item item;

        public DraggedItem(Slot from, Item itemData)
        {
            this.slotData = from;
            this.item = itemData;
        }
    }
}