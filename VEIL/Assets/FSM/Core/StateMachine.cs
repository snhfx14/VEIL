using System;
using System.Collections.Generic;
using UnityEngine;

namespace AulaFSM.Core
{
    /// <summary>
    /// Contrato que todos os estados cumprem.
    /// Cada estado tem 3 momentos: Enter (entrar), Tick (cada frame) e Exit (sair).
    /// </summary>
    public interface IState
    {
        string Name { get; }          // nome mostrado no ecrã / debug
        Color DebugColor { get; }     // cor usada no label e no modelo
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }

    /// <summary>
    /// Classe base genérica para estados. TOwner é quem "possui" o estado
    /// (o Player, o Inimigo, o GameManager...), assim o estado tem acesso direto aos dados do dono.
    /// </summary>
    public abstract class State<TOwner> : IState
    {
        protected readonly TOwner Owner;

        protected State(TOwner owner) { Owner = owner; }

        public abstract string Name { get; }
        public virtual Color DebugColor => Color.white;

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void Exit() { }

        public override string ToString() => Name;
    }

    /// <summary>
    /// Máquina de Estados Finita (FSM) genérica, feita de raiz.
    ///
    /// - Só existe UM estado ativo de cada vez (CurrentState).
    /// - As transições são declaradas fora dos estados (De -> Para + Condição),
    ///   o que deixa os estados simples e todo o "mapa" de comportamento num único sítio.
    /// - "Any transitions" podem disparar a partir de qualquer estado (ex.: Morrer, Levar dano).
    /// - Guarda histórico de transições para debug.
    /// </summary>
    public class StateMachine
    {
        private class Transition
        {
            public readonly IState To;
            public readonly Func<bool> Condition;
            public readonly string Reason;

            public Transition(IState to, Func<bool> condition, string reason)
            {
                To = to;
                Condition = condition;
                Reason = reason;
            }
        }

        private static readonly List<Transition> NoTransitions = new List<Transition>(0);

        private readonly Dictionary<IState, List<Transition>> _transitions = new Dictionary<IState, List<Transition>>();
        private readonly List<Transition> _anyTransitions = new List<Transition>();
        private List<Transition> _currentTransitions = NoTransitions;

        private readonly List<string> _history = new List<string>();
        private const int MaxHistory = 6;

        public string OwnerName { get; }
        public IState CurrentState { get; private set; }
        public IState PreviousState { get; private set; }
        public float TimeInState { get; private set; }
        public IReadOnlyList<string> History => _history;

        /// <summary>Evento disparado em cada mudança: (anterior, novo, motivo).</summary>
        public event Action<IState, IState, string> StateChanged;

        /// <summary>Se true, escreve cada transição na Consola do Unity.</summary>
        public bool LogTransitions = true;

        public StateMachine(string ownerName)
        {
            OwnerName = ownerName;
        }

        // ------------------------------------------------------------------ Configuração

        /// <summary>Transição normal: só é testada quando o estado atual é "from".</summary>
        public void AddTransition(IState from, IState to, Func<bool> condition, string reason = null)
        {
            if (!_transitions.TryGetValue(from, out var list))
            {
                list = new List<Transition>();
                _transitions[from] = list;
            }
            list.Add(new Transition(to, condition, reason ?? $"{from.Name}->{to.Name}"));
        }

        /// <summary>Transição global: é testada em qualquer estado (tem prioridade sobre as normais).</summary>
        public void AddAnyTransition(IState to, Func<bool> condition, string reason = null)
        {
            _anyTransitions.Add(new Transition(to, condition, reason ?? $"*->{to.Name}"));
        }

        // ------------------------------------------------------------------ Execução

        /// <summary>Chamado a cada frame pelo dono (no Update).</summary>
        public void Tick(float deltaTime)
        {
            var transition = FindTransition();
            if (transition != null)
                SetState(transition.To, transition.Reason);

            TimeInState += deltaTime;
            CurrentState?.Tick(deltaTime);
        }

        /// <summary>Muda de estado: Exit do antigo -> Enter do novo.</summary>
        public void SetState(IState newState, string reason = "inicial")
        {
            if (newState == null || newState == CurrentState)
                return;

            CurrentState?.Exit();

            PreviousState = CurrentState;
            CurrentState = newState;
            TimeInState = 0f;

            _currentTransitions = _transitions.TryGetValue(CurrentState, out var list) ? list : NoTransitions;

            string fromName = PreviousState != null ? PreviousState.Name : "(nenhum)";
            AddHistory($"{fromName} → {CurrentState.Name}  ({reason})");
            if (LogTransitions)
                Debug.Log($"<b>[FSM {OwnerName}]</b> {fromName} → <b>{CurrentState.Name}</b>  <i>({reason})</i>");

            CurrentState.Enter();
            StateChanged?.Invoke(PreviousState, CurrentState, reason);
        }

        public bool IsIn(IState state) => CurrentState == state;

        private Transition FindTransition()
        {
            // 1) Transições globais primeiro (por ordem de registo = prioridade)
            foreach (var t in _anyTransitions)
                if (t.To != CurrentState && t.Condition())
                    return t;

            // 2) Transições do estado atual
            foreach (var t in _currentTransitions)
                if (t.Condition())
                    return t;

            return null;
        }

        private void AddHistory(string entry)
        {
            _history.Insert(0, entry);
            if (_history.Count > MaxHistory)
                _history.RemoveAt(_history.Count - 1);
        }
    }
}
