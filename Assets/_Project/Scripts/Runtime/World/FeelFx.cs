using System.Collections;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The shared one-shot effects of a place (a district, or the map): a dust puff for a hit, a ring along the ground when a floor goes
    /// up, sparkles when a building is finished, fireworks. One of these per district keeps the particle systems few: the same systems are
    /// moved to wherever the effect is wanted and fired with <c>Emit</c>.
    /// </summary>
    public sealed class FeelFx : MonoBehaviour
    {
        static readonly Color[] FireworkColours =
        {
            new Color(1f, 0.35f, 0.4f), new Color(1f, 0.85f, 0.25f), new Color(0.35f, 0.9f, 0.55f),
            new Color(0.4f, 0.7f, 1f), new Color(0.85f, 0.5f, 1f), new Color(1f, 0.6f, 0.25f),
        };

        [SerializeField] ParticleSystem dust;
        [SerializeField] ParticleSystem ring;
        [SerializeField] ParticleSystem sparkles;
        [SerializeField] ParticleSystem firework;

        Coroutine show;

        public void Dust(Vector3 point)
        {
            dust.transform.position = point;
            dust.Emit(7);
        }

        /// <summary>A ring of specks spreading out along the ground from the base of a building.</summary>
        public void Ring(Vector3 centre, float radius)
        {
            ring.transform.position = centre + Vector3.up * 0.2f;
            var shape = ring.shape;
            shape.radius = Mathf.Max(0.5f, radius);
            ring.Emit(30);
        }

        /// <summary>Sparkles rising along a building of the given height.</summary>
        public void Sparkles(Vector3 baseCentre, float height, float width)
        {
            sparkles.transform.position = baseCentre + Vector3.up * (height * 0.5f);
            var shape = sparkles.shape;
            shape.scale = new Vector3(width, height, width);
            sparkles.Emit(34);
        }

        /// <summary>Pink hearts rising from a happy animal.</summary>
        public void Hearts(Vector3 point)
        {
            sparkles.transform.position = point;
            var shape = sparkles.shape;
            shape.scale = new Vector3(0.6f, 0.4f, 0.6f);
            sparkles.Emit(new ParticleSystem.EmitParams { startColor = new Color(1f, 0.45f, 0.65f) }, 9);
        }

        /// <summary>White feathers puffing out where startled birds took off.</summary>
        public void Feathers(Vector3 point)
        {
            dust.transform.position = point;
            dust.Emit(new ParticleSystem.EmitParams { startColor = Color.white }, 12);
        }

        /// <summary>One firework burst at <paramref name="point"/> in a random bright colour.</summary>
        public void Burst(Vector3 point)
        {
            firework.transform.position = point;
            var emit = new ParticleSystem.EmitParams { startColor = FireworkColours[Random.Range(0, FireworkColours.Length)] };
            firework.Emit(emit, 60);
        }

        /// <summary>A short show: <paramref name="bursts"/> bursts at random spots around <paramref name="centre"/>, spread over <paramref name="seconds"/>.</summary>
        public void Show(Vector3 centre, float radius, int bursts, float seconds)
        {
            if (show != null) StopCoroutine(show);
            show = StartCoroutine(RunShow(centre, radius, bursts, seconds));
        }

        IEnumerator RunShow(Vector3 centre, float radius, int bursts, float seconds)
        {
            var gap = seconds / Mathf.Max(1, bursts);
            for (var i = 0; i < bursts; i++)
            {
                var offset = Random.insideUnitCircle * radius;
                Burst(centre + new Vector3(offset.x, Random.Range(6f, 12f), offset.y));
                yield return new WaitForSecondsRealtime(gap * Random.Range(0.6f, 1.2f));
            }
            show = null;
        }
    }
}
