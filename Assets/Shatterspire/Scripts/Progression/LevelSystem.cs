using UnityEngine;

namespace Shatterspire
{
    [DisallowMultipleComponent]
    public sealed class LevelSystem : MonoBehaviour, IExperienceReceiver
    {
        private int level = 1;
        private int currentXp;
        private int requiredXp = 30;
        private int pendingChoices;
        public int Level => level;
        public int CurrentXp => currentXp;
        public int RequiredXp => requiredXp;
        public bool HasPendingChoice => pendingChoices > 0;

        private void Start() => GameEvents.RaiseExperienceChanged(level, currentXp, requiredXp);

        public void AddExperience(int amount)
        {
            currentXp += Mathf.Max(0, amount);
            while (currentXp >= requiredXp)
            {
                currentXp -= requiredXp;
                level++;
                requiredXp = Mathf.RoundToInt(requiredXp * 1.22f + 8f);
                pendingChoices++;
                GameEvents.RaiseLevelUp(level);
            }
            GameEvents.RaiseExperienceChanged(level, currentXp, requiredXp);
        }

        public void ConsumeChoice() => pendingChoices = Mathf.Max(0, pendingChoices - 1);
    }
}
