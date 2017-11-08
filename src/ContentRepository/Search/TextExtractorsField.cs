﻿using System;
using System.Collections.ObjectModel;
using SenseNet.ContentRepository;
using SenseNet.ContentRepository.Fields;
using SenseNet.ContentRepository.Schema;
using SenseNet.ContentRepository.Search.Indexing;

namespace SenseNet.Search
{
    [ShortName("TextExtractors")]
    [DataSlot(0, RepositoryDataType.NotDefined, typeof(ReadOnlyDictionary<string, ITextExtractor>))]
    [DefaultFieldSetting(typeof(NullFieldSetting))]
    [DefaultFieldControl("SenseNet.Portal.UI.Controls.ShortText")]
    public class TextExtractorsField : Field
    {
        protected override void ImportData(System.Xml.XmlNode fieldNode, ImportContext context)
        {
            throw new NotSupportedException("The ImportData operation is not supported on TextExtractorsField.");
        }

        protected override void ExportData(System.Xml.XmlWriter writer, ExportContext context)
        {
            // do not export this field, it is autogenerated in the contetn handler
        }
    }
}