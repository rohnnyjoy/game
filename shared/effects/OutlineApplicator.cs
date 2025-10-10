using Godot;

#nullable enable

public partial class OutlineApplicator : Node
{
    [Export]
    public NodePath MeshRootPath { get; set; } = new NodePath("");

    [Export]
    public ShaderMaterial? OutlineMaterial { get; set; }

    // When true, the outline material is set as the primary material
    // and the original material chain is attached as its NextPass.
    // This makes the base material render after the outline, preventing
    // the outline from covering the mesh when depth testing is disabled.
    [Export]
    public bool OutlineAsPrimary { get; set; } = false;

    private ShaderMaterial? _outlineTemplate;
    private Shader? _outlineShader;

    public override void _Ready()
    {
        if (OutlineMaterial == null)
        {
            GD.PushWarning($"{nameof(OutlineApplicator)} on {GetPath()} requires {nameof(OutlineMaterial)}.");
            return;
        }

        _outlineTemplate = (ShaderMaterial)OutlineMaterial.Duplicate();
        _outlineTemplate.ResourceLocalToScene = true;
        _outlineTemplate.NextPass = null;
        _outlineShader = _outlineTemplate.Shader;

        bool useCustomPath = !string.IsNullOrEmpty(MeshRootPath.ToString());
        Node? meshRoot = useCustomPath ? GetNodeOrNull(MeshRootPath) : GetParent();
        if (meshRoot == null)
        {
            GD.PushWarning($"{nameof(OutlineApplicator)} on {GetPath()} could not find node at '{MeshRootPath}'.");
            return;
        }

        ApplyRecursive(meshRoot);
    }

    private void ApplyRecursive(Node node)
    {
        if (node is MeshInstance3D meshInstance)
        {
            ApplyOutline(meshInstance);
        }

        foreach (Node child in node.GetChildren())
        {
            ApplyRecursive(child);
        }
    }

    private void ApplyOutline(MeshInstance3D meshInstance)
    {
        if (_outlineTemplate == null || _outlineShader == null)
        {
            return;
        }

        var mesh = meshInstance.Mesh;
        if (mesh == null)
        {
            return;
        }

        int surfaceCount = mesh.GetSurfaceCount();
        for (int surface = 0; surface < surfaceCount; surface++)
        {
            Material? material = meshInstance.GetSurfaceOverrideMaterial(surface);
            if (material == null)
            {
                material = mesh.SurfaceGetMaterial(surface);
            }

            if (material == null)
            {
                continue;
            }

            Material workingMaterial = material;
            if (!workingMaterial.ResourceLocalToScene)
            {
                workingMaterial = (Material)workingMaterial.Duplicate();
                workingMaterial.ResourceLocalToScene = true;
            }

            if (OutlineAsPrimary)
            {
                if (TryPrependOutline(workingMaterial, out Material newTop))
                {
                    meshInstance.SetSurfaceOverrideMaterial(surface, newTop);
                }
            }
            else
            {
                bool outlineApplied = EnsureOutline(workingMaterial);
                if (outlineApplied)
                {
                    meshInstance.SetSurfaceOverrideMaterial(surface, workingMaterial);
                }
            }
        }
    }

    private bool EnsureOutline(Material material)
    {
        if (_outlineTemplate == null || _outlineShader == null)
        {
            return false;
        }

        switch (material)
        {
            case BaseMaterial3D baseMaterial:
                if (MaterialChainContainsOutline(baseMaterial.NextPass))
                {
                    return false;
                }

                ShaderMaterial outlineForSurface = CreateOutlineInstance();
                outlineForSurface.NextPass = baseMaterial.NextPass;
                baseMaterial.NextPass = outlineForSurface;
                return true;

            case ShaderMaterial shaderMaterial:
                if (shaderMaterial.Shader == _outlineShader || MaterialChainContainsOutline(shaderMaterial.NextPass))
                {
                    return false;
                }

                ShaderMaterial outlineChain = CreateOutlineInstance();
                outlineChain.NextPass = shaderMaterial.NextPass;
                shaderMaterial.NextPass = outlineChain;
                return true;

            default:
                GD.PushWarning($"{nameof(OutlineApplicator)} encountered unsupported material type '{material.GetType().Name}' on {GetPath()}.");
                return false;
        }
    }

    private bool TryPrependOutline(Material material, out Material newTop)
    {
        newTop = material;
        if (_outlineTemplate == null || _outlineShader == null)
        {
            return false;
        }

        // If the top is already the outline, nothing to do.
        if (material is ShaderMaterial smTop && smTop.Shader == _outlineShader)
        {
            return false;
        }

        // Search for an existing outline in the chain to move it to the top.
        Material? prev = null;
        Material? cur = material;
        while (cur != null)
        {
            if (cur is ShaderMaterial sm && sm.Shader == _outlineShader)
            {
                // Detach sm from its current position in the chain.
                Material? after = sm.NextPass;
                if (prev is ShaderMaterial psm)
                {
                    psm.NextPass = after;
                }
                else if (prev is BaseMaterial3D pbm)
                {
                    pbm.NextPass = after;
                }
                // Place outline at the top, followed by the original material chain.
                sm.NextPass = material;
                newTop = sm;
                return true;
            }

            if (cur is ShaderMaterial csm)
            {
                prev = cur;
                cur = csm.NextPass;
            }
            else if (cur is BaseMaterial3D cbm)
            {
                prev = cur;
                cur = cbm.NextPass;
            }
            else
            {
                break;
            }
        }

        // No existing outline in chain; create a new outline instance and make it primary.
        ShaderMaterial outline = CreateOutlineInstance();
        outline.NextPass = material;
        newTop = outline;
        return true;
    }

    private bool MaterialChainContainsOutline(Material? material)
    {
        while (material != null)
        {
            switch (material)
            {
                case ShaderMaterial shaderMaterial:
                    if (shaderMaterial.Shader == _outlineShader)
                    {
                        return true;
                    }

                    material = shaderMaterial.NextPass;
                    break;
                case BaseMaterial3D baseMaterial:
                    material = baseMaterial.NextPass;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    private ShaderMaterial CreateOutlineInstance()
    {
        ShaderMaterial outline = (ShaderMaterial)_outlineTemplate!.Duplicate();
        outline.ResourceLocalToScene = true;
        outline.NextPass = null;
        return outline;
    }
}
