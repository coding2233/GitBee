// See https://aka.ms/new-console-template for more information
//Console.WriteLine("Hello, World!");

using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        new ExcelToProtobuf("Excel/Test.xlsx");
    }
}

public class ExcelToProtobuf
{
    string m_protoOutPath;
    string m_protoVersion = "syntax = \"proto3\";";
    StringBuilder m_stringBuilder;
    public ExcelToProtobuf(string excelPath)
    {
        m_protoOutPath = Path.GetDirectoryName(excelPath);

        using (var stream = new FileStream(excelPath, FileMode.Open))
        {
            stream.Position = 0;
            XSSFWorkbook xssWorkbook = new XSSFWorkbook(stream);
            for (int i = 0; i < xssWorkbook.NumberOfSheets; i++)
            {
                var sheet = xssWorkbook.GetSheetAt(i);
                var msgTemplates = ReadSheet(sheet);
                if (msgTemplates!=null)
                {
                    string protoPath = Path.Combine(m_protoOutPath, sheet.SheetName + ".proto");
                    string jsonPath = Path.Combine(m_protoOutPath, sheet.SheetName + ".json");

                    var protoFile = MessageToProto(msgTemplates, sheet.SheetName);
                    var protoData = MessageToData(msgTemplates, sheet.SheetName);

                    File.WriteAllText(protoPath, protoFile);
                    File.WriteAllText(jsonPath, protoData);
                }
            }
        }
    }

    public string MessageToProto(MessageTemplate message, string sheetName)
    {
        if (m_stringBuilder == null)
        {
            m_stringBuilder = new StringBuilder();
        }
        else
        {
            m_stringBuilder.Clear();
        }

        List<MessageTemplate> messages = new List<MessageTemplate>();
        messages.Add(message);
        //string sheetName = Path.GetFileNameWithoutExtension(protoPath);
        MessageTemplate sheetMessage = new MessageTemplate(sheetName);
        sheetMessage.Types = new List<string>() { $"repeated {message.Name}" };
        sheetMessage.Vars = new List<string>() { message.Name.ToLower()+"_list" };
        sheetMessage.Desc = new List<string>() { sheetName };
        messages.Add(sheetMessage);

        m_stringBuilder.AppendLine(m_protoVersion);
        m_stringBuilder.AppendLine("");
        foreach (var itemMessage in messages)
        {
            m_stringBuilder.Append("message ");
            m_stringBuilder.AppendLine(itemMessage.Name);
            m_stringBuilder.AppendLine("{");
            for (int i = 0; i < itemMessage.Types.Count; i++)
            {
                m_stringBuilder.Append("\t//");
                m_stringBuilder.AppendLine(itemMessage.Desc[i]);
                m_stringBuilder.Append("\t");
                m_stringBuilder.Append(itemMessage.Types[i]);
                m_stringBuilder.Append(" ");
                m_stringBuilder.Append(itemMessage.Vars[i]);
                m_stringBuilder.Append(" = ");
                m_stringBuilder.Append(i+1);
                m_stringBuilder.AppendLine(";");
            }
            m_stringBuilder.AppendLine("}\n");
        }

        //File.WriteAllText(protoPath, m_stringBuilder.ToString());
        return m_stringBuilder.ToString();
    }


    public string MessageToData(MessageTemplate msg, string sheetName)
    {
        StringBuilder stringBuilder = new StringBuilder();
        //stringBuilder.AppendLine("{");
        //stringBuilder.Append("\"");
        stringBuilder.Append(msg.Name.ToLower());
        stringBuilder.Append("_list");
        //stringBuilder.Append("\"");
        stringBuilder.AppendLine(":[");

        for (int d = 0; d < msg.Data.Count; d++)
        {
            var data = msg.Data[d];
            stringBuilder.AppendLine("{");

            for (int i = 0; i < msg.Vars.Count; i++)
            {
                //stringBuilder.Append("\"");
                stringBuilder.Append(msg.Vars[i]);
                //stringBuilder.Append("\"");
                stringBuilder.Append(":");

                string varType = msg.Types[i];
                if (varType.Equals("repeated"))
                {
                    stringBuilder.Append("[");

                    string dataValue = ICellToString(data[i]);
                    string[] dataArgs = dataValue.Split('|');
                    for (int j = 0; j < dataArgs.Length; j++)
                    {
                        if (varType.Contains("string"))
                        {
                            stringBuilder.Append("\"");
                            stringBuilder.Append(dataArgs[j]);
                            stringBuilder.Append("\"");
                        }
                        else
                        {
                            stringBuilder.Append(dataArgs[j]);
                        }

                        if (j < dataArgs.Length - 1)
                        {
                            stringBuilder.Append(",");
                        }
                    }

                    stringBuilder.Append("]");
                }
                else
                {
                    if (varType.Contains("string"))
                    {
                        stringBuilder.Append("\"");
                        stringBuilder.Append(ICellToString(data[i]));
                        stringBuilder.Append("\"");
                    }
                    else
                    {
                        stringBuilder.Append(ICellToString(data[i]));
                    }
                }

                

                if (i < msg.Vars.Count - 1)
                {
                    stringBuilder.Append(",");
                }
            }

            stringBuilder.AppendLine("}");
            if (d < msg.Data.Count - 1)
            {
                stringBuilder.Append(",");
            }
        }

        stringBuilder.AppendLine("]");

        return stringBuilder.ToString();
        //File.WriteAllText(dataPath, stringBuilder.ToString());
    }


    private string ICellToString(ICell cell)
    {
        switch (cell.CellType)
        {
            case CellType.Numeric:
                return cell.NumericCellValue.ToString();
            case CellType.String:
                return cell.StringCellValue;
            case CellType.Formula:
                return cell.CellFormula.ToString();
            case CellType.Blank:
                return "";
            case CellType.Boolean:
                return cell.BooleanCellValue.ToString();
            default:
                return cell.ToString();
        }
        return null;
    }

    private MessageTemplate ReadSheet(ISheet sheet, int rowIndex = 0)
    {

        for (int i = rowIndex; i < sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null)
            {
                continue;
            }

            var cell = row.GetCell(0);
            if (cell != null)
            {
                if ("#message".Equals(cell.StringCellValue))
                {
                    var msgTemplate = ParseMessage(sheet, i, 0);
                    return msgTemplate;
                }
            }
        }

        return null;
    }

    private MessageTemplate ParseMessage(ISheet sheet, int rowIndex, int cellIndex)
    {
        string msgName = sheet.GetRow(rowIndex).GetCell(cellIndex + 1).StringCellValue;
        var msgIdCell = sheet.GetRow(rowIndex).GetCell(cellIndex + 2);
        string msgId = msgIdCell==null? "": msgIdCell.StringCellValue;
        MessageTemplate msgTempalye = new MessageTemplate(msgName, msgId);
        //.proto
        var typeRow = sheet.GetRow(rowIndex + 1);
        var varRow = sheet.GetRow(rowIndex + 2);
        var descRow = sheet.GetRow(rowIndex + 3);
        for (int j = 1; j < typeRow.LastCellNum; j++)
        {
            string typeCellValue = typeRow.GetCell(j).StringCellValue;
            string varCellValue = varRow.GetCell(j).StringCellValue;
            msgTempalye.Types.Add(typeCellValue);
            msgTempalye.Vars.Add(varCellValue);
            msgTempalye.Desc.Add(descRow.GetCell(j).StringCellValue);
           
            //msgTempalye.Data.Add(new List<ICell>());
        }

        //data
        for (int i = rowIndex + 4; i < sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (row == null)
            {
                break;
            }
            List<ICell> cells = new List<ICell>();
            for (int j = 1; j < row.LastCellNum; j++)
            {
                cells.Add(row.GetCell(j));
            }
            msgTempalye.Data.Add(cells);
        }

        return msgTempalye;
    }

}


public class MessageTemplate
{
    public string Name;
    public string Id;
    public List<string> Types;
    public List<string> Vars;
    public List<string> Desc;
    public List<List<ICell>> Data;
    public MessageTemplate(string name, string id=null)
    {
        Name = name;
        Id = id;
        Types = new List<string>();
        Vars = new List<string>();
        Desc = new List<string>();
        Data = new List<List<ICell>>();
    }
}