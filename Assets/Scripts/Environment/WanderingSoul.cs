using UnityEngine;

namespace ParryEverything.Environment
{
    // Âme damnée qui erre dans la zone, loin du joueur comme près de lui :
    // elle marche courbée, s'arrête, s'agenouille un long moment, ou s'efface dans la brume
    // pour réapparaître ailleurs. Rien ne dépend de la caméra ni du joueur.
    public class WanderingSoul : MonoBehaviour
    {
        private enum State { Walk, Pause, Kneel, Fade }

        [SerializeField] private MeshFilter ground;
        [SerializeField] private Renderer body;                                 // matériau CatSoul (_Alpha)
        [SerializeField] private Vector2 xRange = new Vector2(-55f, 55f);
        [SerializeField] private Vector2 depthRange = new Vector2(4f, 12f);
        [SerializeField] private Vector2 walkSpeed = new Vector2(0.18f, 0.4f);
        [SerializeField] private float alpha = 0.22f;

        private State state;
        private float stateLeft;
        private float speed;
        private float direction = 1f;
        private float fade = 1f;          // 1 : visible, 0 : effacée
        private bool fadingIn;
        private float kneel;              // 0 : debout, 1 : à genoux
        private float gait;               // phase de la marche
        private Vector3 baseScale;
        private MaterialPropertyBlock block;

        private void Start()
        {
            baseScale = transform.localScale;
            block = new MaterialPropertyBlock();
            direction = Random.value < 0.5f ? -1f : 1f;
            gait = Random.value * 10f;
            Enter(State.Walk);
            stateLeft *= Random.value;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            stateLeft -= dt;

            switch (state)
            {
                case State.Walk:
                    Walk(dt);
                    if (stateLeft <= 0f) EnterNext();
                    break;
                case State.Pause:
                    if (stateLeft <= 0f) EnterNext();
                    break;
                case State.Kneel:
                    if (stateLeft <= 0f) Enter(State.Walk);
                    break;
                case State.Fade:
                    UpdateFade(dt);
                    break;
            }

            kneel = Mathf.MoveTowards(kneel, state == State.Kneel ? 1f : 0f, dt * 0.4f);
            ApplyPose();
        }

        private void EnterNext()
        {
            float r = Random.value;
            if (r < 0.35f) Enter(State.Pause);
            else if (r < 0.6f) Enter(State.Kneel);
            else if (r < 0.75f) Enter(State.Fade);
            else Enter(State.Walk);
        }

        private void Enter(State next)
        {
            state = next;
            switch (next)
            {
                case State.Walk:
                    stateLeft = Random.Range(8f, 25f);
                    speed = Random.Range(walkSpeed.x, walkSpeed.y);
                    if (Random.value < 0.4f) direction = -direction;
                    break;
                case State.Pause:
                    stateLeft = Random.Range(3f, 12f);
                    break;
                case State.Kneel:
                    stateLeft = Random.Range(10f, 30f);
                    break;
                case State.Fade:
                    fadingIn = false;
                    break;
            }
        }

        private void Walk(float dt)
        {
            Vector3 p = transform.position;
            p.x += direction * speed * dt;
            if (p.x < xRange.x || p.x > xRange.y)
            {
                direction = -direction;
                p.x = Mathf.Clamp(p.x, xRange.x, xRange.y);
            }
            if (GroundHeight.TryGet(ground, p.x, p.z, out float y)) p.y = y;
            transform.position = p;
            gait += dt * speed * 9f;
            Quaternion look = Quaternion.LookRotation(new Vector3(direction, 0f, 0f), Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 60f * dt);
        }

        // S'efface, réapparaît ailleurs dans la zone, puis revient lentement
        private void UpdateFade(float dt)
        {
            if (!fadingIn)
            {
                fade = Mathf.MoveTowards(fade, 0f, dt / 3f);
                if (fade > 0f) return;
                float x = Random.Range(xRange.x, xRange.y);
                float z = Random.Range(depthRange.x, depthRange.y);
                if (GroundHeight.TryGet(ground, x, z, out float y)) transform.position = new Vector3(x, y, z);
                fadingIn = true;
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 1f, dt / 4f);
                if (fade >= 1f) Enter(State.Pause);
            }
        }

        private void ApplyPose()
        {
            // Marche : léger balancement ; à genoux : écrasée et penchée en avant
            bool moving = state == State.Walk;
            float bob = moving ? Mathf.Abs(Mathf.Sin(gait)) * 0.04f : 0f;
            float sway = moving ? Mathf.Sin(gait) * 3f : 0f;
            transform.localScale = new Vector3(baseScale.x, baseScale.y * (Mathf.Lerp(1f, 0.62f, kneel) + bob), baseScale.z);
            Vector3 e = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(Mathf.Lerp(0f, 18f, kneel), e.y, sway);

            if (body != null)
            {
                body.GetPropertyBlock(block);
                block.SetFloat("_Alpha", alpha * fade);
                block.SetFloat("_HaloStrength", 0.25f * fade);
                body.SetPropertyBlock(block);
            }
        }
    }
}
