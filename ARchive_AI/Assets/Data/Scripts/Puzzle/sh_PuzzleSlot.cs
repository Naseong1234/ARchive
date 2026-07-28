using UnityEngine;

public sealed class sh_PuzzleSlot : MonoBehaviour
{
    [SerializeField] private int slotNumber = 1;

    public int SlotNumber => slotNumber;
    public bool IsValid => slotNumber > 0;

    public void SetSlotNumber(int newSlotNumber)
    {
        slotNumber = Mathf.Max(1, newSlotNumber);
    }

    private void OnValidate()
    {
        slotNumber = Mathf.Max(1, slotNumber);
    }
}
