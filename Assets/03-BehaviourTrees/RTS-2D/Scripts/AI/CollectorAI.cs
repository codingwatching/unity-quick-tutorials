using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

using BehaviorTree;

namespace Tutorial03_BehaviourTrees.RTS_2D {

    public class CollectorAI : BTree
    {

        public enum Resource
        {
            Wood,
            Minerals,
        }

        private const float _COLLECT_RATE = 0.25f;
        private const float _DELIVER_DELAY = 0.35f;

        [Header("Move")]
        [SerializeField] private Tilemap _groundTilemap;
        [SerializeField] private float _speed = 3;

        [Header("Collect")]
        [SerializeField] private Resource _resource;
        [SerializeField] private int _maxStorage = 10;
        [SerializeField] private Transform _resourceFillBar;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Sprite _horizontalSprite;
        [SerializeField] private Sprite _verticalSprite;

        private void Start()
        {
            _UpdateResourceFillBar();
        }

        protected override Node SetupTree()
        {
            // check for ground tilemap reference + prepare pathfinder
            if (_groundTilemap == null)
            {
                Debug.LogWarning($"No ground tilemap for '{name}'");
                return null;
            }

            AStar.Pathfinder2D pathfinder = new AStar.Pathfinder2D(_groundTilemap);

            // get resource/storage maps
            Tilemap resourceTilemap = null, storageTilemap = null;
            ResourceMap resourceMap = null;
            Tilemap[] tilemaps = FindObjectsOfType<Tilemap>();
            foreach (Tilemap t in tilemaps)
            {
                if (t.name == $"Resource:{_resource}")
                {
                    resourceTilemap = t;
                    resourceMap = t.GetComponent<ResourceMap>();
                }
                else if (t.name == $"Buildings:{_resource}")
                {
                    storageTilemap = t;
                }
            }

            if (resourceTilemap == null)
            {
                Debug.LogWarning($"Cannot find resource tilemap for resource '{_resource}'");
                return null;
            }

            if (storageTilemap == null)
            {
                Debug.LogWarning($"Cannot find buildings tilemap for resource '{_resource}'");
                return null;
            }

            BTree.current = this; // used to easily reference this tree in all nodes
                                // during the whole initialisation phase

            Node root = new Selector();

            root.SetChildren(new List<Node>()
            {
                new Sequence(new List<Node>()
                {
                    new CheckReachedMaxStorage(_maxStorage),
                    new Selector(new List<Node>()
                    {
                        new Inverter(new List<Node>() { new CheckHasTarget() }),
                        new CheckTargetIsResource()
                    }),
                    new TaskFindClosestTarget(transform, storageTilemap, false)
                }),
                new Sequence(new List<Node>()
                {
                    new CheckHasTarget(),
                    new Selector(new List<Node>()
                    {
                        new Sequence(new List<Node>()
                        {
                            new CheckInTargetRange(transform),
                            new Selector(new List<Node>()
                            {
                                new Sequence(new List<Node>()
                                {
                                    new CheckTargetIsResource(),
                                    new Timer(_COLLECT_RATE, new List<Node>()
                                    {
                                        new TaskCollect(resourceMap, _maxStorage),
                                    }, _UpdateResourceFillBar)
                                }),
                                new Timer(_DELIVER_DELAY, new List<Node>()
                                {
                                    new TaskDeliver(_resource.ToString())
                                }, _UpdateResourceFillBar),
                                new Sequence(new List<Node>()
                                {
                                    new CheckIsVisible(_spriteRenderer),
                                    new TaskEnterBuilding(this)
                                })
                            })
                        }),
                        new TaskWalk(transform, pathfinder, _speed, (Vector2 dir) =>
                        {
                            if (Mathf.Approximately(dir.x, 1f))
                            {
                                // going right
                                _spriteRenderer.sprite = _horizontalSprite;
                                _spriteRenderer.flipX = false;
                                _spriteRenderer.flipY = false;
                            }
                            else if (Mathf.Approximately(dir.x, -1f))
                            {
                                // going left
                                _spriteRenderer.sprite = _horizontalSprite;
                                _spriteRenderer.flipX = true;
                                _spriteRenderer.flipY = false;
                            }
                            else if (Mathf.Approximately(dir.y, 1f))
                            {
                                // going up
                                _spriteRenderer.sprite = _verticalSprite;
                                _spriteRenderer.flipX = false;
                                _spriteRenderer.flipY = true;
                            }
                            else if (Mathf.Approximately(dir.y, -1f))
                            {
                                // going down
                                _spriteRenderer.sprite = _verticalSprite;
                                _spriteRenderer.flipX = false;
                                _spriteRenderer.flipY = false;
                            }
                        }),
                    })
                }),
                new TaskFindClosestTarget(transform, resourceTilemap, true),
                new TaskFindClosestTarget(transform, storageTilemap, false)
            }, root);

            blackboard["current_resource_amount"] = 0;

            BTree.current = null;

            return root;
        }

        private void _UpdateResourceFillBar()
        {
            int curAmount = (int)blackboard["current_resource_amount"];
            float resourceRatio = curAmount / (float)_maxStorage;
            _resourceFillBar.localScale = new Vector3(resourceRatio, 1, 1);
            _resourceFillBar.localPosition = new Vector3(-0.5f + resourceRatio / 2f, 0, 0);
        }

        public void ToggleVisuals(bool on)
        {
            _spriteRenderer.enabled = on;
            _resourceFillBar.parent.gameObject.SetActive(on);
        }
    }

}
