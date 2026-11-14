using System;
using NUnit.Framework;
using ParryEverything.Core;
using UnityEngine;

namespace ParryEverything.Tests
{
    // Entrées : chaque action a une touche, la reconfiguration se remet à zéro.
    public class GameInputTests
    {
        [TearDown]
        public void TearDown()
        {
            GameInput.ResetBindings();
        }

        [Test]
        public void EveryAction_HasAKey()
        {
            foreach (GameAction action in Enum.GetValues(typeof(GameAction)))
            {
                Assert.Greater(GameInput.Keys(action).Count, 0, $"{action} sans touche");
            }
        }

        [Test]
        public void Rebind_ThenReset()
        {
            GameInput.Rebind(GameAction.Parry, KeyCode.K);
            Assert.AreEqual(KeyCode.K, GameInput.Keys(GameAction.Parry)[0]);
            Assert.AreEqual("K", GameInput.KeyName(GameAction.Parry));

            GameInput.ResetBindings();
            Assert.AreEqual(KeyCode.X, GameInput.Keys(GameAction.Parry)[0]);
        }

        [Test]
        public void KeyName_IsReadable()
        {
            Assert.AreEqual("Entrée", GameInput.KeyName(GameAction.Confirm));
            Assert.AreEqual("Échap", GameInput.KeyName(GameAction.Pause));
        }
    }
}
