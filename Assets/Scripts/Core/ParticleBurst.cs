using UnityEngine;

namespace ParryEverything.Core
{
    // Gerbe de particules ponctuelle (étincelles, éclats, braises), détruite automatiquement à la fin.
    // direction (facultatif) : jet en cône d'ouverture coneAngle dans cette direction, au lieu d'une gerbe dans tous les sens.
    public static class ParticleBurst
    {
        public static ParticleSystem Spawn(
            string name,
            Vector3 position,
            ParticleSystem.MinMaxGradient color,
            int count,
            Vector2 lifetime,
            Vector2 speed,
            Vector2 size,
            float gravity,
            float radius,
            float duration = 0.3f,
            Vector3? direction = null,
            float coneAngle = 25f)
        {
            GameObject go = new GameObject(name);
            go.transform.position = position;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            // Le système démarre tout seul à l'ajout : on l'arrête pour pouvoir le configurer
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = duration;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            if (direction.HasValue && direction.Value.sqrMagnitude > 1e-6f)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = coneAngle;
                shape.radius = radius;
                go.transform.rotation = Quaternion.LookRotation(direction.Value.normalized);
            }
            else
            {
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = radius;
            }

            // Les particules rétrécissent jusqu'à disparaître
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ps.GetComponent<ParticleSystemRenderer>().material = MaterialFactory.SharedUnlit;
            ps.Play();
            return ps;
        }
    }
}
