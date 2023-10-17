using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;


// TODO: Aseprite export to vid script...
// https://github.com/redboyrave/Aseprite-GB-Exporter/blob/main/gb-export.lua


namespace PGImageTool
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void SelectImagesButton_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    SrcFilesListView.Clear();

                    foreach (string s in openFileDialog.FileNames)
                    {
                        SrcFilesListView.Items.Add(s);
                    }
                }
                catch (Exception)
                {
                }
            }
        }

        private void SelectOutputButton_Click(object sender, EventArgs e)
        {
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                OutputFileTextBox.Text = saveFileDialog.FileName;
            }
        }


        void Write_File_Preamble(CodeGenContext con, string filename)
        {
            con.sbcpp.Append("#include \"" + Path.GetFileName(con.Header_File_From_Cpp()) + "\"\r\n\r\n");

            // Header
            con.sbh.Append("#ifndef " + con.Header_Guard() + "\r\n");
            con.sbh.Append("#define " + con.Header_Guard() + "\r\n\r\n");

            con.sbh.Append("#include \"PixelGrid.h\"\r\n\r\n");
        }

        void Write_Image_Preamble(CodeGenContext con, string imagename)
        {
            con.sbcpp.Append("static const pgcolor " + con.Array_Name(imagename) + "[] = {\r\n");
            con.sbh.Append("extern PGImage " + con.Obj_Name(imagename) + ";\r\n");
        }


        private void GenerateButton_Click(object sender, EventArgs e)
        {
            string filename = OutputFileTextBox.Text;
            CodeGenContext con = new CodeGenContext(filename);

            Write_File_Preamble(con, filename);

            foreach (ListViewItem item in SrcFilesListView.Items)
            {
                string imagename = item.Text;

                // Preamble for each file
                Write_Image_Preamble(con, imagename);

                // open the file and read into memory
                using (Bitmap bitmap = new Bitmap(imagename))
                {

                    int bytecounter = 0;
                    int rowcounter = 0;
                    con.sbcpp.Append("\t");

                    for (int j = 0; j < bitmap.Height; ++j)
                    {
                        for (int i = 0; i < bitmap.Width; ++i)
                        {
                            Color pixel = bitmap.GetPixel(i, j);

                            //con.sbcpp.Append("0x" + Convert.ToString(pixel.A, 16).PadLeft(2, '0') + ",");
                            //con.sbcpp.Append("0x" + Convert.ToString(pixel.B, 16).PadLeft(2, '0') + ",");
                            //con.sbcpp.Append("0x" + Convert.ToString(pixel.G, 16).PadLeft(2, '0') + ",");
                            //con.sbcpp.Append("0x" + Convert.ToString(pixel.R, 16).PadLeft(2, '0') );

                            con.sbcpp.Append("0x" + Convert.ToString(pixel.A, 16).PadLeft(2, '0') + Convert.ToString(pixel.R, 16).PadLeft(2, '0') + Convert.ToString(pixel.G, 16).PadLeft(2, '0') + Convert.ToString(pixel.B, 16).PadLeft(2, '0'));

                            // comma after every byte unless last byte
                            bytecounter++;
                            if ((j < bitmap.Height - 1) || (i < bitmap.Width - 1))
                            {
                                con.sbcpp.Append(",");
                            }
                        }
                        // carriage return after every row unless its the last row
                        if (j < bitmap.Height - 1)
                        {
                            con.sbcpp.Append("\r\n\t");
                        }
                    }

                    Write_Image_Postamble(con, imagename, bitmap.Width, bitmap.Height);
                }
            }
            Write_File_Postamble(con, filename);

            con.Generate_Files();
            StatusLabel.Text = "Generated Files!"; // MessageBox.Show("Success! Generated Files:\r\n" + con.OutputCppName + "\r\n" + con.Header_File_From_Cpp());
            StatusLabel.Visible = true;
            ClearStatustimer.Stop();
            ClearStatustimer.Start();
        }

        void Write_Image_Postamble(CodeGenContext con, string imagename, int w,int h)
        {
            con.sbcpp.Append("\r\n};\r\n");
            con.sbcpppost.Append("PGImage " + con.Obj_Name(imagename) + "((uint8_t*)" + con.Array_Name(imagename) + "," + w.ToString() + "," + h.ToString() + ");\r\n");
        }
        void Write_File_Postamble(CodeGenContext con, string filename)
        {
            // write all the object declarations at the bottom of the file.
            con.sbcpp.Append("\r\n\r\n");
            con.sbcpp.Append(con.sbcpppost);
            con.sbcpp.Append("\r\n\r\n");
            con.sbcpp.Append("\r\n\r\n");

            // Header file
            con.sbh.Append("\r\n#endif //" + con.Header_Guard() + "\r\n");
        }

        private void ClearStatustimer_Tick(object sender, EventArgs e)
        {
            StatusLabel.Text = "";
            ClearStatustimer.Stop();
        }

        public static byte[] StructToByteArray<T>(T obj) where T : struct
        {
            int size = Marshal.SizeOf(obj);
            byte[] byteArray = new byte[size];
            IntPtr ptr = Marshal.AllocHGlobal(size);

            Marshal.StructureToPtr(obj, ptr, true);
            Marshal.Copy(ptr, byteArray, 0, size);
            Marshal.FreeHGlobal(ptr);

            return byteArray;
        }


        private void MakeVideo_Click(object sender, EventArgs e)
        {
            // open the output file with .vid extension.
            string basename = SrcFilesListView.Items[0].Text;
            basename = Path.ChangeExtension(basename,null);
            basename = Regex.Replace(basename, @"\d+$", "");  // \d+$ matches one or more digits (\d) at the end of the string ($),
            string vidname = basename + ".fxv";

            // sort the listview into ascending order?
            //SrcFilesListView.Sorting = SortOrder.Ascending;
            //SrcFilesListView.Sort();
            int w = 0;
            int h = 0;
            byte[] pixelbits = new byte[] { 0, 0, 0, 0 };

            using (FileStream vidstream = new FileStream(vidname, FileMode.Create, FileAccess.Write))
            {
                // Loop over each frame and append the pixel data directly to the file
                foreach (ListViewItem item in SrcFilesListView.Items)
                {
                    string imagename = item.Text;

                    // open the file and read into memory
                    using (Bitmap bitmap = new Bitmap(imagename))
                    {
                        // Let the first bitmap define the dimensions of the video (all frames must be the same size)
                        // We write the header into the FXV file here as well
                        if (w == 0)
                        {
                            w = bitmap.Width;
                            h = bitmap.Height;

                            FXVHeader header = new FXVHeader((ushort)w, (ushort)h);
                            byte[] headerbytes = StructToByteArray(header);
                            vidstream.Write(headerbytes,0,headerbytes.Length);
                        }

                        // Read w x h pixels from the image (use black for edge cases if we have incorrectly sized frames)
                        for (int j = 0; j < h; ++j)
                        {
                            for (int i = 0; i < w; ++i)
                            {
                                Color pixel = Color.FromArgb(0, 0, 0, 0);
                                if ((i < w) && (j < h))
                                {
                                    pixel = bitmap.GetPixel(i, j);
                                }
                                pixelbits[0] = pixel.B;
                                pixelbits[1] = pixel.G;
                                pixelbits[2] = pixel.R;
                                pixelbits[3] = pixel.A;

                                vidstream.Write(pixelbits, 0, 4);
                            }
                        }
                    }
                }
            }
            StatusLabel.Text = "Generated Video:" + vidname;
            StatusLabel.Visible = true;
            ClearStatustimer.Stop();
            ClearStatustimer.Start();
        }

        private void Make_FXI_Button_Click(object sender, EventArgs e)
        {

            byte[] pixelbits = new byte[] { 0, 0, 0, 0 };

            // Loop over each frame and append the pixel data directly to the file
            int count = SrcFilesListView.Items.Count;
            foreach (ListViewItem item in SrcFilesListView.Items)
            {
                string imagename = item.Text;
                string outname = Path.ChangeExtension(imagename, ".fxi");

                using (FileStream imgstream = new FileStream(outname, FileMode.Create, FileAccess.Write))
                {
                    // open the file and read into memory
                    using (Bitmap bitmap = new Bitmap(imagename))
                    {
                        // get the size of this bitmap
                        int w = bitmap.Width;
                        int h = bitmap.Height;

                        // write the FXI header into the file
                        FXIHeader header = new FXIHeader((ushort)w, (ushort)h);
                        byte[] headerbytes = StructToByteArray(header);
                        imgstream.Write(headerbytes, 0, headerbytes.Length);

                        // Read pixels from the image and write them to the file.
                        for (int j = 0; j < h; ++j)
                        {
                            for (int i = 0; i < w; ++i)
                            {
                                Color pixel = Color.FromArgb(0, 0, 0, 0);
                                if ((i < w) && (j < h))
                                {
                                    pixel = bitmap.GetPixel(i, j);
                                }
                                pixelbits[0] = pixel.B;
                                pixelbits[1] = pixel.G;
                                pixelbits[2] = pixel.R;
                                pixelbits[3] = pixel.A;

                                imgstream.Write(pixelbits, 0, 4);
                            }
                        }
                    }
                }
            }
            
            StatusLabel.Text = "Generated " + count.ToString() + "fxi files";
            StatusLabel.Visible = true;
            ClearStatustimer.Stop();
            ClearStatustimer.Start();
        }
    }
}
