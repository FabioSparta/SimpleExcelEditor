using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using OfficeOpenXml;
using ClosedXML.Excel;
using System.Security.AccessControl;
using System.Security.Permissions;
using System.Collections;

namespace MaeilExercise
{
    public partial class btn_saveChanges : Form
    {

        //#################################### Global Variables  ###########################################
        Dictionary<String, String[]> rows = new Dictionary<string, string[]>(); // key(rowId) : value(all cells values) 
        ArrayList logs = new ArrayList();
        DataTable dt = new DataTable();
        String filePath;
        int pageNumber = 0;
        int pageSize = 20 ;
        int maxRowId = 0;
        int numOfColumns = 0;


        public btn_saveChanges()
        {
            InitializeComponent();
        }



        //#################################### Event Listeners  ###########################################

        private void btn_loadFile_Click(object sender, EventArgs e)
        {
            // Choose file
            openFileDialog1.ShowDialog();
            filePath = openFileDialog1.FileName;
            System.Diagnostics.Debug.WriteLine("File Opened: " + filePath);
            label_error.Text = "Loading file...Please wait.";

            // Check Extension and load file
            string fileExt = Path.GetExtension(filePath);
            if (fileExt == ".xlsx" )
            {   
                LoadExcel(filePath);
                ShowRows();
            }
            else
            {
                label_error.Text = "Only .xlsx files are supported";
            }
        }

        private void btn_next_Click(object sender, EventArgs e)
        {
            saveChanges();
            dt.Clear();
            pageNumber++;
            ShowRows();

        }

        private void btn_prev_Click(object sender, EventArgs e)
        {
            saveChanges();
            dt.Clear();
            if (pageNumber > 0)
                pageNumber--;
            ShowRows();

        }

        private void btn_first_Click(object sender, EventArgs e)
        {
            saveChanges();
            dt.Clear();
            pageNumber = 0;
            ShowRows();
        }

        private void btn_last_Click(object sender, EventArgs e)
        {
            saveChanges();
            dt.Clear();
            pageNumber = maxRowId / pageSize;
            ShowRows();

        }
        private void btn_saveFile_Click(object sender, EventArgs e)
        {
            saveExcelFile();
            saveLogsAsync();
        }

        private void btn_delete_Click(object sender, EventArgs e)
        {
            deleteSelectedRows();
        }


        //#################################### FUNCTIONS ###########################################

        private void ShowRows()
        {
            int start = pageNumber * pageSize;
            for(int i = pageNumber*pageSize; i < start + pageSize;i++) { 
                // Add the row to the rows collection.
                if (rows.ContainsKey(i.ToString()))
                {
                    DataRow newRow = dt.NewRow();
                    newRow.ItemArray = rows[i.ToString()];
                    dt.Rows.Add(newRow);
                }
            }
        }

        protected void LoadExcel(string filePath)
        {
            // Open the Excel file
            using (XLWorkbook workBook = new XLWorkbook(filePath))
            {
                // Read the first Sheet from Excel file.
                IXLWorksheet workSheet = workBook.Worksheet(1);
                numOfColumns = workSheet.Columns().Count() +1 ;

                // Loop through the Worksheet rows.
                bool firstRow = true;
                foreach (IXLRow row in workSheet.Rows())
                {
                    // Use the first row to add columns to DataTable.
                    if (firstRow)
                    {
                        dt.Columns.Add("#");
                        foreach (IXLCell cell in row.Cells())
                        {
                            dt.Columns.Add(cell.Value.ToString());
                        }
                        firstRow = false;
                    }
                    else
                    {
                        if (!row.IsEmpty())
                        {
                            string[] rowValues = new string[numOfColumns];
                            
                            for (int i=1; i<numOfColumns; i++ )
                                rowValues[i] = row.Cells(false).ElementAt(i-1).Value.ToString(); 
                            
                            string pos = maxRowId.ToString();
                            rowValues[0] = pos;
                            rows.Add(pos,rowValues);
                            maxRowId++;
                            if (maxRowId == 5000) // TODO: DELETE THIS LATER
                                break;
                        }
                        
                    }
                    dataGridView1.DataSource = dt;
                }
            }
            // Make first column not editable
            foreach (DataGridViewColumn dc in dataGridView1.Columns)
            {
                if (dc.Index.Equals(0)) {dc.ReadOnly = true;}
            }
            label_error.Text = "Finished Loading File!";

        }

        private void saveChanges()
        {
            foreach (DataRow dr in dt.Rows)
            {
                string pos = dr[0].ToString();
                string[] rowValues = new string[numOfColumns];
                string[] tableRow = string.Join(",", dr.ItemArray).Split(',').ToArray();

                // If exists, just update
                if (rows.ContainsKey(pos))
                {
                    if (!rows[pos].SequenceEqual(tableRow))
                    {
                        string rowOldContent = string.Join(";", rows[pos][1..].ToArray());
                        string rowNewContent = string.Join(";", tableRow[1..].ToArray());
                        string log = DateTime.Now + "-  UPDATE:  " + "FROM - " + rowOldContent + " - TO " + rowNewContent;
                        logs.Add(log);
                        rows[pos] = tableRow;
                    }
                }
                // Verify if it is empty. If not empty, save to map 
               else 
                {
                    foreach (string s in tableRow)
                    {
                        if (!s.Equals(""))
                        {
                            // Add to Map
                            pos = (++maxRowId).ToString();
                            rowValues[0] = pos;
                            for (int x = 1; x < numOfColumns; x++)
                                rowValues[x] = tableRow[x];
                            rows.Add(pos, rowValues);
                            // Add log
                            string rowContent = string.Join(";", rowValues[1..].ToArray());
                            string log = DateTime.Now + " -  ADD:  "  + rowValues[1..];
                            logs.Add(log);
                            break;
                        }
                    }
                       
                }
            }
        }

        private void deleteSelectedRows()
        {
            foreach (DataGridViewCell oneCell in dataGridView1.SelectedCells)
            {
                if (oneCell.Selected)
                {
                    string pos = oneCell.OwningRow.Cells[0].Value.ToString();
                    dataGridView1.Rows.RemoveAt(oneCell.RowIndex);
                    if (rows.ContainsKey(pos))
                    {
                        // Add log
                        string rowContent =  string.Join(";", rows[pos][1..].ToArray());
                        string log = DateTime.Now + " -  DELETE:  " + rowContent;
                        logs.Add(log);

                        rows.Remove(pos);
                    }
                }
            }
        }

        private async Task saveLogsAsync()
        {
            IEnumerable<string> xLogs =logs.Cast<string>().Select(log=>log);

            StreamWriter sw = new StreamWriter(Application.StartupPath + "\\Logs\\" + filePath.Split("\\").Last());
            foreach (string s in logs)
                sw.WriteLine(s);
            sw.Close();
            //string path = Directory.GetCurrentDirectory() + "\\LogsFile.txt";
            //await File.WriteAllLinesAsync(path,  xLogs);
        }

        private void saveExcelFile()
        {
            using (var workbook = new XLWorkbook())
            {
                DataTable table = new DataTable();

                // New table with all columns except the 1st -> "#"
                string[] columns = dt.Columns.Cast<DataColumn>().Select(x => x.ColumnName).ToArray();
                for (int i = 1; i<columns.Length;  i++) 
                    table.Columns.Add(columns[i]);

                // Load all Map Values to the datatable
                foreach (KeyValuePair<string, string[]> entry in rows)
                {
                    DataRow newRow = dt.NewRow();
                    newRow.ItemArray = entry.Value[1..];
                    dt.Rows.Add(newRow);
                }

                

                // Save Datatable in Excel File
                var worksheet = workbook.Worksheets.Add(dt, "sheetdata");
                string path = Application.StartupPath +"\\SavedFiles\\" +  filePath.Split("\\").Last();
                System.Diagnostics.Debug.WriteLine("Right before saving excel: " + path);
                System.Diagnostics.Debug.WriteLine("Right before saving excel, number of rows to save : " + dt.Rows.Count);
                workbook.SaveAs(path);
            }
        }
    }
}
