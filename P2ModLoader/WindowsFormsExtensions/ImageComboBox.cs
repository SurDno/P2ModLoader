namespace P2ModLoader.WindowsFormsExtensions;

public class ImageComboBox : ComboBox {
	public Func<object, Image?>? GetImageForItem { get; set; }
	public int ImgHeight;
	
	public ImageComboBox(int imgHeight) {
		DrawMode = DrawMode.OwnerDrawFixed;
		DropDownStyle = ComboBoxStyle.DropDownList;
		ItemHeight = ImgHeight = imgHeight;
	}

	protected override void OnDrawItem(DrawItemEventArgs e) {
		if (e.Index < 0) return;

		e.DrawBackground();
		e.DrawFocusRectangle();

		var item = Items[e.Index];
		var image = GetImageForItem?.Invoke(item);
        
		if (image != null) 
			e.Graphics.DrawImage(image, e.Bounds.Left + 2, e.Bounds.Top + 2, ImgHeight, ImgHeight);
        
		var displayText = GetItemText(item);
		e.Graphics.DrawString(displayText, e.Font, new SolidBrush(e.ForeColor), 
			e.Bounds.Left + ImgHeight + 4 , e.Bounds.Top + ImgHeight / 8);

		base.OnDrawItem(e);
	}
}