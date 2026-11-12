using UnityEngine;

public class camera : MonoBehaviour
{
    public Transform player; // Reference to the player's transform

    // Tremblement en cours (temps réel, pour marcher même quand le jeu est figé)
    private float shakeTimeLeft = 0f;
    private float shakeMagnitude = 0f;

    // Cadrage large (ex : combat de boss) : la caméra recule et monte, en douceur
    private float baseZ;
    private bool baseZSet = false;
    private float extraDistance = 0f, extraDistanceTarget = 0f;
    private float yOffset = 0f, yOffsetTarget = 0f;

    public void SetFraming(float distance, float height)
    {
        extraDistanceTarget = distance;
        yOffsetTarget = height;
    }

    // Lance un tremblement d'écran : durée en secondes, amplitude en unités
    public void Shake(float duration, float magnitude)
    {
        shakeTimeLeft = duration;
        shakeMagnitude = magnitude;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // Check if the player reference is assigned
        if (player != null)
        {
            // Get the player's position
            Vector3 playerPosition = player.position;

            if (!baseZSet)
            {
                baseZ = transform.position.z;
                baseZSet = true;
            }
            // Temps réel : le cadrage bouge aussi pendant les ralentis
            float k = 1f - Mathf.Exp(-1.5f * Time.unscaledDeltaTime);
            extraDistance = Mathf.Lerp(extraDistance, extraDistanceTarget, k);
            yOffset = Mathf.Lerp(yOffset, yOffsetTarget, k);

            // Set the camera's position to follow the player
            transform.position = new Vector3(playerPosition.x, playerPosition.y + yOffset, baseZ - extraDistance);
        }

        // Décalage aléatoire autour de la position suivie
        if (shakeTimeLeft > 0f)
        {
            Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
            transform.position += new Vector3(offset.x, offset.y, 0f);
            shakeTimeLeft -= Time.unscaledDeltaTime;
        }
        
    }
}
