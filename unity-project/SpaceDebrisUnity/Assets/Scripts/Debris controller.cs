using UnityEngine;

public class DebrisController : MonoBehaviour
{
    public int debrisCount = 5000;
    public float minOrbitRadius = 6f;
    public float maxOrbitRadius = 11f;
    public Transform earth; // Drag your Earth object here

    private ParticleSystem debrisSystem;
    private ParticleSystem.Particle[] particles;
    private DebrisOrbitalData[] debrisData;

    // A struct to hold the unique orbital data for each piece of debris
    private struct DebrisOrbitalData
    {
        public float semiMajorAxis;
        public float semiMinorAxis;
        public float orbitalSpeed;
        public float angleOffset;
        public Quaternion orbitalPlane;
    }

    void Start()
    {
        debrisSystem = GetComponent<ParticleSystem>();
        particles = new ParticleSystem.Particle[debrisCount];
        debrisData = new DebrisOrbitalData[debrisCount];

        var main = debrisSystem.main;
        main.maxParticles = debrisCount;

        debrisSystem.Emit(debrisCount);
        debrisSystem.GetParticles(particles);

        for (int i = 0; i < debrisCount; i++)
        {
            float a = Random.Range(minOrbitRadius, maxOrbitRadius); // Semi-major axis
            float e = Random.Range(0f, 0.4f); // Eccentricity
            float b = a * Mathf.Sqrt(1 - (e * e)); // Semi-minor axis

            float inclination = Random.Range(0f, Mathf.PI);
            float longitude = Random.Range(0f, Mathf.PI * 2f);

            debrisData[i] = new DebrisOrbitalData
            {
                semiMajorAxis = a,
                semiMinorAxis = b,
                orbitalSpeed = Random.Range(0.05f, 0.1f),
                angleOffset = Random.Range(0f, Mathf.PI * 2f),
                orbitalPlane = Quaternion.Euler(
                    Mathf.Rad2Deg * inclination,
                    Mathf.Rad2Deg * longitude,
                    0
                )
            };
        }
    }

    void Update()
    {
        for (int i = 0; i < debrisCount; i++)
        {
            float angle = (Time.time * debrisData[i].orbitalSpeed) + debrisData[i].angleOffset;

            // Calculate position in a flat 2D ellipse
            float x = debrisData[i].semiMajorAxis * Mathf.Cos(angle);
            float y = debrisData[i].semiMinorAxis * Mathf.Sin(angle);

            Vector3 position = new Vector3(x, y, 0);

            // Rotate the position by the randomized orbital plane
            particles[i].position = debrisData[i].orbitalPlane * position;
        }

        // Apply the calculated positions back to the particle system
        debrisSystem.SetParticles(particles, debrisCount);
    }
}
