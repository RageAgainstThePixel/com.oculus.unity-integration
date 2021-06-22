using Oculus.Avatar;

public class OvrAvatarBase : OvrAvatarComponent
{
    private ovrAvatarBaseComponent component = new ovrAvatarBaseComponent();

    private void Update()
    {
        if (owner == null)
        {
            return;
        }

        if (CAPI.ovrAvatarPose_GetBaseComponent(owner.sdkAvatar, ref component))
        {
            UpdateAvatar(component.renderComponent);
        }
        else
        {
            owner.Base = null;
            Destroy(this);
        }
    }
}
