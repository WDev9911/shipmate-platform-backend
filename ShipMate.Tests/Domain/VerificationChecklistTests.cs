using ShipMate.Domain.Constants;
using ShipMate.Domain.Entities;

namespace ShipMate.Tests.Domain;

public class VerificationChecklistTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = Now.AddHours(1);
    private readonly Guid _user = Guid.NewGuid();

    private static VerificationChecklist New() =>
        VerificationChecklist.CreateFor(new MvpSnapshot { WorkspaceId = Guid.NewGuid(), Version = 3 });

    private VerificationChecklist TickedExcept(string leftOut)
    {
        var checklist = New();
        foreach (var key in VerificationItemKeys.All.Where(k => k != leftOut))
        {
            checklist.Check(key, true, null, _user, Now);
        }

        return checklist;
    }

    [Fact]
    public void CreateFor_HasTheFiveItems_AllApplicableAndUnchecked()
    {
        var snapshot = new MvpSnapshot { WorkspaceId = Guid.NewGuid(), Version = 3 };

        var checklist = VerificationChecklist.CreateFor(snapshot);

        Assert.Equal(snapshot.Id, checklist.MvpSnapshotId);
        Assert.Equal(snapshot.WorkspaceId, checklist.WorkspaceId);
        Assert.Equal(3, checklist.SnapshotVersion);
        Assert.Equal(VerificationItemKeys.All, checklist.Items.Select(i => i.ItemKey).ToList());
        Assert.All(checklist.Items, i =>
        {
            Assert.Equal(checklist.Id, i.ChecklistId);
            Assert.True(i.Applicable);
            Assert.False(i.Checked);
        });
        Assert.False(checklist.AllItemsChecked);
        Assert.Null(checklist.CompletedAt);
    }

    [Fact]
    public void Check_TheLastApplicableItem_CompletesTheList_AndSetsCompletedAt()
    {
        var checklist = TickedExcept(VerificationItemKeys.BasicCrud);

        var result = checklist.Check(VerificationItemKeys.BasicCrud, true, null, _user, Later);

        Assert.Equal(ChecklistTransition.Completed, result.Transition);
        Assert.True(checklist.AllItemsChecked);
        Assert.Equal(Later, checklist.CompletedAt);
    }

    [Fact]
    public void Uncheck_AfterCompletion_ReopensIt_AndClearsCompletedAt()
    {
        var checklist = TickedExcept("none");

        var result = checklist.Check(VerificationItemKeys.BasicCrud, false, null, _user, Later);

        Assert.Equal(ChecklistTransition.Reopened, result.Transition);
        Assert.False(checklist.AllItemsChecked);
        Assert.Null(checklist.CompletedAt);
    }

    [Fact]
    public void Check_SameValueTwice_ReportsNoTransition()
    {
        var checklist = TickedExcept("none");

        var result = checklist.Check(VerificationItemKeys.BasicCrud, true, null, _user, Later);

        Assert.Equal(ChecklistTransition.None, result.Transition);
        Assert.Equal(Now, checklist.CompletedAt);
    }

    [Fact]
    public void Check_RecordsWhoAndWhen_AndTheNote()
    {
        var checklist = New();

        checklist.Check(VerificationItemKeys.MainUserJourney, true, "  Booked, confirmed, viewed  ", _user, Now);

        var item = checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.MainUserJourney);
        Assert.True(item.Checked);
        Assert.Equal(_user, item.CheckedByUserId);
        Assert.Equal(Now, item.CheckedAt);
        Assert.Equal("Booked, confirmed, viewed", item.Note);
    }

    [Fact]
    public void Check_WithoutANote_KeepsTheOldOne_AndABlankNoteClearsIt()
    {
        var checklist = New();
        checklist.Check(VerificationItemKeys.BasicCrud, true, "first", _user, Now);

        checklist.Check(VerificationItemKeys.BasicCrud, true, null, _user, Later);
        Assert.Equal("first", checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.BasicCrud).Note);

        checklist.Check(VerificationItemKeys.BasicCrud, true, "  ", _user, Later);
        Assert.Null(checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.BasicCrud).Note);
    }

    [Fact]
    public void Uncheck_ClearsWhoAndWhen_ButKeepsTheNote()
    {
        var checklist = New();
        checklist.Check(VerificationItemKeys.BasicCrud, true, "worked", _user, Now);

        checklist.Check(VerificationItemKeys.BasicCrud, false, null, _user, Later);

        var item = checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.BasicCrud);
        Assert.False(item.Checked);
        Assert.Null(item.CheckedByUserId);
        Assert.Null(item.CheckedAt);
        Assert.Equal("worked", item.Note);
    }

    [Fact]
    public void Check_ANotApplicableItem_IsRejected()
    {
        var checklist = New();
        checklist.SetApplicability(VerificationItemKeys.Authentication, false, "No accounts in this product", Now);

        var result = checklist.Check(VerificationItemKeys.Authentication, true, null, _user, Later);

        Assert.Equal(ChecklistEditError.ItemNotApplicable, result.Error);
        Assert.False(checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.Authentication).Checked);
    }

    [Fact]
    public void Uncheck_ANotApplicableItem_DoesNothing()
    {
        var checklist = New();
        checklist.SetApplicability(VerificationItemKeys.Authentication, false, "No accounts in this product", Now);

        var result = checklist.Check(VerificationItemKeys.Authentication, false, null, _user, Later);

        Assert.Equal(ChecklistEditError.None, result.Error);
        Assert.Equal(ChecklistTransition.None, result.Transition);
    }

    [Fact]
    public void MarkNotApplicable_TheLastUncheckedItem_CompletesTheList()
    {
        var checklist = TickedExcept(VerificationItemKeys.Authentication);

        var result = checklist.SetApplicability(VerificationItemKeys.Authentication, false, "No accounts in this product", Later);

        Assert.Equal(ChecklistTransition.Completed, result.Transition);
        Assert.True(checklist.AllItemsChecked);
        Assert.Equal("No accounts in this product", checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.Authentication).NotApplicableReason);
    }

    [Fact]
    public void MarkNotApplicable_MainUserJourney_IsRejected()
    {
        var checklist = New();

        var result = checklist.SetApplicability(VerificationItemKeys.MainUserJourney, false, "Not for this product", Now);

        Assert.Equal(ChecklistEditError.MainJourneyRequired, result.Error);
        Assert.True(checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.MainUserJourney).Applicable);
    }

    [Fact]
    public void MarkApplicableAgain_UnchecksIt_AndReopens()
    {
        var checklist = TickedExcept(VerificationItemKeys.Authentication);
        checklist.SetApplicability(VerificationItemKeys.Authentication, false, "No accounts in this product", Now);

        var result = checklist.SetApplicability(VerificationItemKeys.Authentication, true, null, Later);

        Assert.Equal(ChecklistTransition.Reopened, result.Transition);
        var item = checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.Authentication);
        Assert.True(item.Applicable);
        Assert.Null(item.NotApplicableReason);
        Assert.False(item.Checked);
    }

    [Fact]
    public void SayingAgainWhatIsAlreadySo_DoesNotWipeATick()
    {
        var checklist = New();
        checklist.Check(VerificationItemKeys.BasicCrud, true, null, _user, Now);

        checklist.SetApplicability(VerificationItemKeys.BasicCrud, true, null, Later);

        Assert.True(checklist.Items.Single(i => i.ItemKey == VerificationItemKeys.BasicCrud).Checked);
    }

    [Fact]
    public void MissingItems_SkipsNotApplicable_AndKeepsTheFixedOrder()
    {
        var checklist = New();
        checklist.Check(VerificationItemKeys.MainUserJourney, true, null, _user, Now);
        checklist.SetApplicability(VerificationItemKeys.Authentication, false, "No accounts in this product", Now);

        Assert.Equal(
            [VerificationItemKeys.ProductionConfig, VerificationItemKeys.BasicCrud, VerificationItemKeys.ErrorHandling],
            checklist.MissingItems());
    }

    [Fact]
    public void EveryEdit_RaisesTheRevision()
    {
        var checklist = New();
        Assert.Equal(0, checklist.Revision);

        checklist.Check(VerificationItemKeys.BasicCrud, true, null, _user, Now);
        checklist.SetApplicability(VerificationItemKeys.Authentication, false, "No accounts in this product", Now);
        checklist.Check(VerificationItemKeys.BasicCrud, true, null, _user, Later);

        Assert.Equal(3, checklist.Revision);
    }
}
