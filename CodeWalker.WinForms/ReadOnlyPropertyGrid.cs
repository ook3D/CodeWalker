using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CodeWalker.WinForms
{
    public partial class ReadOnlyPropertyGrid : PropertyGridFix
    {
        public ReadOnlyPropertyGrid()
        {
            InitializeComponent();
            Disposed += (_, _) => ClearReadOnlyProvider();
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            base.OnPaint(pe);
        }



        private bool _readOnly = true;

        [Browsable(true)]
        [Category("Appearance")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public bool ReadOnly
        {
            get { return _readOnly; }
            set
            {
                _readOnly = value;
                SetObjectAsReadOnly(SelectedObject, _readOnly);
            }
        }

        private TypeDescriptionProvider? provider;
        private object? providedObject;

        protected override void OnSelectedObjectsChanged(EventArgs e)
        {
            SetObjectAsReadOnly(SelectedObject, _readOnly);
            base.OnSelectedObjectsChanged(e);
        }

        private void ClearReadOnlyProvider()
        {
            if (provider != null && providedObject != null)
            {
                TypeDescriptor.RemoveProvider(provider, providedObject);
            }
            provider = null;
            providedObject = null;
        }

        private void SetObjectAsReadOnly(object? selectedObject, bool isReadOnly)
        {
            ClearReadOnlyProvider();
            if (isReadOnly && selectedObject != null)
            {
                provider = TypeDescriptor.AddAttributes(selectedObject, ReadOnlyAttribute.Yes);
                providedObject = selectedObject;
            }
            Refresh();
        }
    }
}
