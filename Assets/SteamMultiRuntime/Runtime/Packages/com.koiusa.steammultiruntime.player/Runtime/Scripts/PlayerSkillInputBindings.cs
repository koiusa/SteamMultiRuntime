using System;
using Koiusa.Input;

namespace Koiusa.SteamMultiRuntime
{
    internal sealed class PlayerSkillInputBindings : IDisposable
    {
        private const string AttackActionPath = "Combat/Attack";
        private const string DashActionPath = "Player/Dash";
        private const string GuardActionPath = "Player/Guard";
        private const string HealActionPath = "Player/Heal";

        private readonly InputActionsConfig inputActionsConfig;
        private readonly Action attackPerformed;
        private readonly Action dashPerformed;
        private readonly Action guardPerformed;
        private readonly Action guardCanceled;
        private readonly Action healPerformed;

        private InputActionBinding attackBinding;
        private InputActionBinding dashBinding;
        private InputActionBinding guardBinding;
        private InputActionBinding healBinding;

        internal PlayerSkillInputBindings(
            InputActionsConfig inputActionsConfig,
            Action attackPerformed,
            Action dashPerformed,
            Action guardPerformed,
            Action guardCanceled,
            Action healPerformed)
        {
            this.inputActionsConfig = inputActionsConfig;
            this.attackPerformed = attackPerformed;
            this.dashPerformed = dashPerformed;
            this.guardPerformed = guardPerformed;
            this.guardCanceled = guardCanceled;
            this.healPerformed = healPerformed;
        }

        internal void Acquire()
        {
            if (attackBinding != null || dashBinding != null || guardBinding != null || healBinding != null)
                return;

            attackBinding = Bind(AttackActionPath, attackPerformed);
            dashBinding = Bind(DashActionPath, dashPerformed);
            guardBinding = InputActionBinding.Bind(
                inputActionsConfig?.FindAction(GuardActionPath),
                _ => guardPerformed?.Invoke(),
                _ => guardCanceled?.Invoke());
            healBinding = Bind(HealActionPath, healPerformed);
        }

        public void Dispose()
        {
            attackBinding?.Dispose();
            dashBinding?.Dispose();
            guardBinding?.Dispose();
            healBinding?.Dispose();
            attackBinding = null;
            dashBinding = null;
            guardBinding = null;
            healBinding = null;
        }

        private InputActionBinding Bind(string actionPath, Action callback)
        {
            return InputActionBinding.Bind(
                inputActionsConfig?.FindAction(actionPath),
                _ => callback?.Invoke());
        }
    }
}
