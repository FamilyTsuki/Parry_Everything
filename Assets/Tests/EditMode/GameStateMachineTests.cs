using NUnit.Framework;
using ParryEverything.Core;

namespace ParryEverything.Tests
{
    // Flux de jeu : transitions permises et refusées.
    public class GameStateMachineTests
    {
        [Test]
        public void StartsInMenu_ThenPlays()
        {
            var machine = new GameStateMachine();

            Assert.AreEqual(GameState.Menu, machine.State);
            Assert.IsTrue(machine.StartGame());
            Assert.AreEqual(GameState.Playing, machine.State);
        }

        [Test]
        public void Pause_TogglesOnlyWhilePlaying()
        {
            var machine = new GameStateMachine(GameState.Playing);

            Assert.IsTrue(machine.TogglePause());
            Assert.AreEqual(GameState.Paused, machine.State);
            Assert.IsTrue(machine.TogglePause());
            Assert.AreEqual(GameState.Playing, machine.State);

            machine.PlayerDied();
            Assert.IsFalse(machine.TogglePause(), "Pas de pause pendant la mort");
            Assert.AreEqual(GameState.Dead, machine.State);
        }

        [Test]
        public void Death_ThenRespawn()
        {
            var machine = new GameStateMachine(GameState.Playing);

            Assert.IsTrue(machine.PlayerDied());
            Assert.IsFalse(machine.PlayerDied(), "Une seule mort à la fois");
            Assert.IsTrue(machine.Respawn());
            Assert.AreEqual(GameState.Playing, machine.State);
        }

        [Test]
        public void CannotDieWhilePaused()
        {
            var machine = new GameStateMachine(GameState.Paused);

            Assert.IsFalse(machine.PlayerDied());
            Assert.AreEqual(GameState.Paused, machine.State);
        }

        [Test]
        public void Finish_ThenBackToMenu()
        {
            var machine = new GameStateMachine(GameState.Playing);

            Assert.IsTrue(machine.Finish());
            Assert.AreEqual(GameState.End, machine.State);
            Assert.IsFalse(machine.StartGame(), "On repasse par le menu");
            Assert.IsTrue(machine.ReturnToMenu());
            Assert.AreEqual(GameState.Menu, machine.State);
        }

        [Test]
        public void Changed_ReportsPreviousAndNext()
        {
            var machine = new GameStateMachine();
            GameState from = GameState.End, to = GameState.End;
            int calls = 0;
            machine.Changed += (a, b) => { from = a; to = b; calls++; };

            machine.StartGame();
            machine.Finish();
            machine.StartGame(); // refusé : pas d'événement

            Assert.AreEqual(2, calls);
            Assert.AreEqual(GameState.Playing, from);
            Assert.AreEqual(GameState.End, to);
        }
    }
}
