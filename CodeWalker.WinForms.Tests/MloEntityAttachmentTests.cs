using CodeWalker.GameFiles;
using CodeWalker.Project;
using CodeWalker.Project.Panels;
using Xunit;

namespace CodeWalker.WinForms.Tests;

public class MloEntityAttachmentTests
{
    [Theory]
    [InlineData(0, -1, -1)]
    [InlineData(-1, 0, -1)]
    [InlineData(-1, -1, 0)]
    public void NewEntityAppearsUnderItsAttachmentAndSurvivesSaving(int roomIndex, int portalIndex, int setIndex)
    {
        StaThread.Run(() =>
        {
            var ytyp = new YtypFile { Name = "attachment_test.ytyp" };
            var mlo = new MloArchetype { Ytyp = ytyp };
            mlo.rooms = [new MCMloRoomDef { OwnerMlo = mlo, RoomName = "room" }];
            mlo.portals = [new MCMloPortalDef { OwnerMlo = mlo }];
            mlo.entitySets = [new MCMloEntitySet { OwnerMlo = mlo }];
            ytyp.AllArchetypes = [mlo];
            var project = new ProjectFile { YtypFiles = [ytyp] };
            using var panel = new ProjectExplorerPanel(null!);
            panel.LoadProjectTree(project);

            Assert.True(mlo.AddEntity(new YmapEntityDef(), roomIndex, portalIndex, setIndex));
            var entity = setIndex >= 0 ? mlo.entitySets[setIndex].Entities[0] : mlo.entities[0];
            var node = panel.AddMloEntityTreeNode(entity);

            Assert.NotNull(node);
            Assert.Same(entity, node.Tag);
            Assert.Same(node, panel.FindMloEntityTreeNode(entity));
            object parent = roomIndex >= 0 ? mlo.rooms[roomIndex]
                : portalIndex >= 0 ? mlo.portals[portalIndex] : mlo.entitySets[setIndex];
            Assert.Same(parent, node.Parent!.Tag);

            var reloaded = new YtypFile();
            reloaded.Load(ytyp.Save());
            var savedMlo = Assert.IsType<MloArchetype>(Assert.Single(reloaded.AllArchetypes));
            if (portalIndex >= 0) Assert.Equal(0u, Assert.Single(savedMlo.portals[portalIndex].AttachedObjects));
            else if (roomIndex >= 0) Assert.Equal(0u, Assert.Single(savedMlo.rooms[roomIndex].AttachedObjects));
            else Assert.Single(savedMlo.entitySets[setIndex].Entities);
        });
    }
}
