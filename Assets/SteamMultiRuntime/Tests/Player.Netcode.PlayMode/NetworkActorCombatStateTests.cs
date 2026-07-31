using NUnit.Framework;
using UnityEngine;

namespace Koiusa.SteamMultiRuntime.Tests
{
    public sealed class NetworkActorCombatStateTests
    {
        [Test]
        public void ReplicatedDamageRaisesHealthChangedAndDeathNotifications()
        {
            var actor = new GameObject("ReplicatedCombatActor");
            try
            {
                var health = actor.AddComponent<ActorHealthFeature>();
                var respawn = actor.AddComponent<ActorRespawnFeature>();
                var healthChangedCount = 0;
                var diedCount = 0;
                var lifeStateChangedCount = 0;
                var reportedDead = false;

                health.HealthChanged += (_, _) => healthChangedCount++;
                health.Died += _ => diedCount++;
                respawn.LifeStateChanged += isDead =>
                {
                    lifeStateChangedCount++;
                    reportedDead = isDead;
                };

                health.ApplyReplicatedHealth(80f);
                health.ApplyReplicatedHealth(0f);

                Assert.That(healthChangedCount, Is.EqualTo(2));
                Assert.That(diedCount, Is.EqualTo(1));
                Assert.That(lifeStateChangedCount, Is.EqualTo(1));
                Assert.That(reportedDead, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void FirstHealthReadReturnsInitializedValue()
        {
            var actor = new GameObject("HealthInitializationActor");
            try
            {
                var health = actor.AddComponent<ActorHealthFeature>();

                Assert.That(health.CurrentHealth, Is.EqualTo(100f));
                Assert.That(health.IsAlive, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void ReplicatedDeathReplacesRendererShader()
        {
            var actor = new GameObject("DeathPresentationActor");
            try
            {
                var health = actor.AddComponent<ActorHealthFeature>();
                actor.AddComponent<ActorRespawnFeature>();
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.transform.SetParent(actor.transform, false);
                var renderer = visual.GetComponent<Renderer>();
                actor.AddComponent<ActorSkillEffectVisual>();
                actor.AddComponent<GuardShieldVisual>();
                actor.AddComponent<ActorDeathPresentation>();

                health.ApplyReplicatedHealth(0f);

                var dissolveShader = Resources.Load<Shader>("Shaders/CharacterDeathDissolve");
                Assert.That(dissolveShader, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.shader, Is.SameAs(dissolveShader));
                var shieldRenderer = actor.transform.Find("GuardShieldVisual").GetComponent<Renderer>();
                Assert.That(shieldRenderer.sharedMaterial.shader, Is.Not.SameAs(dissolveShader));
            }
            finally
            {
                Object.DestroyImmediate(actor);
            }
        }
    }
}
