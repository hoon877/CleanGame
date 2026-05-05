using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyRemovalFx : MonoBehaviour
{
    private bool isPlaying;
    private float elapsed;
    private Vector3 startScale;
    private Vector3 startPosition;
    private Renderer[] renderers;
    private Material[] runtimeMaterials;
    private readonly List<Transform> particles = new List<Transform>();

    public void Play()
    {
        if (isPlaying)
        {
            return;
        }

        isPlaying = true;
        elapsed = 0f;
        startScale = transform.localScale;
        startPosition = transform.position;
        renderers = GetComponentsInChildren<Renderer>(true);
        runtimeMaterials = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            runtimeMaterials[i] = renderers[i].material;
        }

        for (int i = 0; i < 8; i++)
        {
            GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            particle.name = "Removal Particle";
            particle.transform.position = transform.position + new Vector3(Random.Range(-0.25f, 0.25f), Random.Range(0.1f, 0.6f), Random.Range(-0.25f, 0.25f));
            particle.transform.localScale = Vector3.one * Random.Range(0.06f, 0.12f);
            Renderer particleRenderer = particle.GetComponent<Renderer>();
            particleRenderer.material.color = new Color(1f, 0.95f, 0.82f, 0.9f);
            Destroy(particle.GetComponent<Collider>());
            particle.transform.SetParent(transform.parent, true);
            particles.Add(particle.transform);
        }
    }

    private void Update()
    {
        if (!isPlaying)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / 0.32f);
        transform.localScale = Vector3.Lerp(startScale, startScale * 0.7f, t);
        transform.position = Vector3.Lerp(startPosition, startPosition + Vector3.up * 0.18f, t);

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Color color = runtimeMaterials[i].color;
            color.a = Mathf.Lerp(1f, 0f, t);
            runtimeMaterials[i].color = color;
        }

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            if (particles[i] == null)
            {
                particles.RemoveAt(i);
                continue;
            }

            particles[i].position += new Vector3(Random.Range(-0.15f, 0.15f), 0.8f, Random.Range(-0.15f, 0.15f)) * Time.deltaTime;
            particles[i].localScale *= 0.985f;
            Renderer particleRenderer = particles[i].GetComponent<Renderer>();
            if (particleRenderer != null)
            {
                Color color = particleRenderer.material.color;
                color.a = Mathf.Lerp(0.9f, 0f, t);
                particleRenderer.material.color = color;
            }
        }

        if (t >= 1f)
        {
            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i] != null)
                {
                    Destroy(particles[i].gameObject);
                }
            }
            gameObject.SetActive(false);
        }
    }
}
