using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ParryEverything.UI
{
    // Ligne de l'écran d'options : « Nom      ‹ valeur › ».
    // ← → (clavier, plus tard manette) changent la valeur ; clic sur une flèche aussi ; clic sur la ligne : valeur suivante.
    // Avec une barre (volumes) : clic sur la barre pour choisir directement.
    public class OptionRow
    {
        private readonly Func<string> text;
        private readonly Func<float> ratio;            // null : pas de barre
        private readonly Action<int> step;              // -1 / +1
        private readonly Action<float> setRatio;        // clic sur la barre (null : pas de barre)
        private readonly Label value;
        private readonly VisualElement fill;
        private readonly Label percent;

        public Button Root { get; }
        // Valeur changée par le joueur (son, sauvegarde des options)
        public event Action Changed;

        // Ligne à valeurs (Oui/Non, résolution)
        public OptionRow(string name, Func<string> text, Action<int> step)
            : this(name, text, step, null, null) { }

        // Ligne à barre (volume)
        public OptionRow(string name, Func<float> ratio, Action<int> step, Action<float> setRatio)
            : this(name, () => Mathf.RoundToInt(ratio() * 100f) + " %", step, ratio, setRatio) { }

        private OptionRow(string name, Func<string> text, Action<int> step, Func<float> ratio, Action<float> setRatio)
        {
            this.text = text;
            this.step = step;
            this.ratio = ratio;
            this.setRatio = setRatio;

            Root = new Button { text = string.Empty };
            Root.AddToClassList("option-row");
            Root.Add(new Label(name) { pickingMode = PickingMode.Ignore }.WithClass("option-name"));

            var control = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("option-control");
            Label left = new Label("‹").WithClass("option-arrow");
            Label right = new Label("›").WithClass("option-arrow");
            left.RegisterCallback<PointerDownEvent>(e => { Step(-1); e.StopPropagation(); });
            right.RegisterCallback<PointerDownEvent>(e => { Step(1); e.StopPropagation(); });

            control.Add(left);
            if (ratio != null)
            {
                var bar = new VisualElement().WithClass("option-bar");
                fill = new VisualElement { pickingMode = PickingMode.Ignore }.WithClass("option-bar-fill");
                bar.Add(fill);
                bar.RegisterCallback<PointerDownEvent>(e =>
                {
                    float w = bar.layout.width;
                    if (w > 0f) Set(Mathf.Clamp01(e.localPosition.x / w));
                    e.StopPropagation();
                });
                control.Add(bar);
                percent = new Label { pickingMode = PickingMode.Ignore }.WithClass("option-percent");
                control.Add(percent);
            }
            else
            {
                value = new Label { pickingMode = PickingMode.Ignore }.WithClass("option-value");
                control.Add(value);
            }
            control.Add(right);
            Root.Add(control);

            Root.clicked += () => Step(1);
            // ← → changent la valeur au lieu de déplacer le focus
            Root.RegisterCallback<NavigationMoveEvent>(e =>
            {
                if (e.direction != NavigationMoveEvent.Direction.Left && e.direction != NavigationMoveEvent.Direction.Right) return;
                Step(e.direction == NavigationMoveEvent.Direction.Left ? -1 : 1);
                e.StopPropagation();
                Root.focusController?.IgnoreEvent(e);
            }, TrickleDown.TrickleDown);

            Refresh();
        }

        public void Refresh()
        {
            if (value != null) value.text = text();
            if (percent != null) percent.text = text();
            if (fill != null) fill.style.width = Length.Percent(ratio() * 100f);
        }

        private void Step(int direction)
        {
            step(direction);
            Refresh();
            Changed?.Invoke();
        }

        private void Set(float r)
        {
            setRatio?.Invoke(r);
            Refresh();
            Changed?.Invoke();
        }
    }
}
