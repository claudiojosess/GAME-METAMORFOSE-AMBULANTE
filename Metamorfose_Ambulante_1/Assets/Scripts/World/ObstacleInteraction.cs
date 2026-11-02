using System.Collections.Generic;
using UnityEngine;

namespace AeroMorphET
{
    public sealed class ObstacleInteraction : MonoBehaviour
    {
        public AlienMorphController.MorphState requiredState = AlienMorphController.MorphState.Solid;
        private bool resolved;

        private void OnTriggerEnter(Collider other)
        {
            if (resolved) return;

            var morph = other.GetComponentInParent<AlienMorphController>();
            if (morph == null) return;

            resolved = true;

            if (morph.CurrentState == requiredState)
            {
                GameManager.Instance?.RegisterCorrectBarrier();
                GameAudio.Play("Acerto");
                ResolveCorrectState();
            }
            else
            {
                GameManager.Instance?.ApplyPenalty();
                GameManager.Instance?.GameOver(MismatchMessage(requiredState));
            }
        }

        private void ResolveCorrectState()
        {
            switch (requiredState)
            {
                case AlienMorphController.MorphState.Solid:
                    // O sólido realmente "quebra" a barreira em pedaços.
                    BreakIntoFragments();
                    break;

                case AlienMorphController.MorphState.Liquid:
                    // Mantém o portal visível por tempo suficiente para a poça rastejar por baixo.
                    SetCollisionEnabled(false);
                    Destroy(gameObject, 2.2f);
                    break;

                case AlienMorphController.MorphState.Gas:
                    // Mantém a grade/turbina visível para as moléculas atravessarem entre os espaços.
                    SetCollisionEnabled(false);
                    Destroy(gameObject, 2.6f);
                    break;
            }
        }

        private void BreakIntoFragments()
        {
            SetCollisionEnabled(false);

            var parts = new List<GameObject>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                parts.Add(renderer.gameObject);

            Vector3 blastCenter = transform.position + Vector3.back * .4f;

            foreach (GameObject part in parts)
            {
                if (part == null) continue;

                part.transform.SetParent(null, true);

                var body = part.AddComponent<Rigidbody>();
                body.mass = .28f;
                body.linearDamping = .12f;
                body.angularDamping = .08f;
                body.AddExplosionForce(8.5f, blastCenter, 5f, 1.1f, ForceMode.Impulse);
                body.AddTorque(Random.insideUnitSphere * 3.5f, ForceMode.Impulse);

                Destroy(part, 1.8f);
            }

            Destroy(gameObject, .06f);
        }

        private void SetCollisionEnabled(bool value)
        {
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = value;
        }

        private static string MismatchMessage(AlienMorphController.MorphState state) => state switch
        {
            AlienMorphController.MorphState.Solid => "Era preciso ficar SÓLIDO para romper o obstáculo de impacto.",
            AlienMorphController.MorphState.Liquid => "Era preciso ficar LÍQUIDO para se achatar e passar pelo vão rasteiro.",
            _ => "Era preciso ficar GASOSO para dispersar as moléculas e atravessar a barreira."
        };

        public static string StateName(AlienMorphController.MorphState state) => state switch
        {
            AlienMorphController.MorphState.Solid => "SÓLIDO",
            AlienMorphController.MorphState.Liquid => "LÍQUIDO",
            _ => "GASOSO"
        };
    }
}
