using System;
using UnityEngine;

namespace BenScr.MinecraftClone
{

    public abstract class ItemData : ScriptableObject
    {
        [Header("General")]

        public int id;
        public Sprite sprite;
        public ushort stackSize = 16;
        [SerializeField] internal string _name;
        public string Name => _name;
        internal string NameEnglish;

        public string description;
        public ItemType type = ItemType.None;

        [Header("Duration")]
        public bool durable;
        public int maxDuration;

        [Header("Positioning")]
        public Vector2 offset;
        public Vector2 size = new Vector2(0.55f, 0.55f);
        public Vector3 rotation;
        public Hand hand;

        [Header("Prizing")]
        public PrizeData prizeData;
        public bool isFree => prizeData.prizeItems == null || prizeData.prizeAmounts == null || prizeData.prizeItems.Length == 0;
    }
    [System.Flags]
    public enum ItemType
    {
        None = 0,
        Weapon = 1 << 0,
        Armor = 1 << 1,
        Potion = 1 << 2,
        QuestItem = 1 << 3,
    }

    [Serializable]
    public struct PrizeData
    {
        public ItemData[] prizeItems;
        public int[] prizeAmounts;
    }

    public enum Hand { Left, Right, Both }
}