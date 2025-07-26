using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.Reflection;

namespace UNSInfra.Services.V1.SparkplugB;

/// <summary>
/// Simplified Sparkplug B payload implementation for common use cases.
/// For full Sparkplug B specification compliance, consider using the official protobuf definitions.
/// </summary>
public sealed partial class SparkplugBPayload : IMessage<SparkplugBPayload>
{
    private static readonly MessageParser<SparkplugBPayload> _parser = new MessageParser<SparkplugBPayload>(() => new SparkplugBPayload());
    private static readonly FieldCodec<Types.Metric> _repeated_metrics_codec = FieldCodec.ForMessage(10, Types.Metric.Parser);
    private readonly RepeatedField<Types.Metric> metrics_ = new RepeatedField<Types.Metric>();

    public static MessageParser<SparkplugBPayload> Parser => _parser;

    public ulong Timestamp { get; set; }
    
    public RepeatedField<Types.Metric> Metrics => metrics_;

    public MessageDescriptor Descriptor => throw new NotImplementedException("Simplified implementation");

    public int CalculateSize()
    {
        int size = 0;
        if (Timestamp != 0UL) size += 1 + CodedOutputStream.ComputeUInt64Size(Timestamp);
        size += metrics_.CalculateSize(_repeated_metrics_codec);
        return size;
    }

    public SparkplugBPayload Clone() => new SparkplugBPayload(this);

    public SparkplugBPayload() { }

    public SparkplugBPayload(SparkplugBPayload other) : this()
    {
        Timestamp = other.Timestamp;
        metrics_.Add(other.metrics_);
    }

    public bool Equals(SparkplugBPayload? other) => ReferenceEquals(this, other);

    public override bool Equals(object? obj) => Equals(obj as SparkplugBPayload);

    public override int GetHashCode() => 0;

    public void MergeFrom(SparkplugBPayload other)
    {
        if (other.Timestamp != 0UL) Timestamp = other.Timestamp;
        metrics_.Add(other.metrics_);
    }

    public void MergeFrom(CodedInputStream input)
    {
        uint tag;
        while ((tag = input.ReadTag()) != 0)
        {
            switch (tag)
            {
                case 8:
                    Timestamp = input.ReadUInt64();
                    break;
                case 18:
                    metrics_.AddEntriesFrom(input, _repeated_metrics_codec);
                    break;
                default:
                    input.SkipLastField();
                    break;
            }
        }
    }

    public void WriteTo(CodedOutputStream output)
    {
        if (Timestamp != 0UL)
        {
            output.WriteRawTag(8);
            output.WriteUInt64(Timestamp);
        }
        metrics_.WriteTo(output, _repeated_metrics_codec);
    }

    public static class Types
    {
        public sealed partial class Metric : IMessage<Metric>
        {
            private static readonly MessageParser<Metric> _parser = new MessageParser<Metric>(() => new Metric());

            public static MessageParser<Metric> Parser => _parser;

            public string Name { get; set; } = string.Empty;
            public ulong Alias { get; set; }
            public ulong Timestamp { get; set; }
            public uint Datatype { get; set; }
            public bool IsHistorical { get; set; }
            public bool IsTransient { get; set; }
            public bool IsNull { get; set; }
            public ulong IntValue { get; set; }
            public ulong LongValue { get; set; }
            public float FloatValue { get; set; }
            public double DoubleValue { get; set; }
            public bool BooleanValue { get; set; }
            public string StringValue { get; set; } = string.Empty;
            public ByteString? BytesValue { get; set; }
            public DataSet? DatasetValue { get; set; }
            public Template? TemplateValue { get; set; }

            public bool HasAlias => Alias != 0;
            public bool HasTimestamp => Timestamp != 0;
            public bool HasIsHistorical => IsHistorical;
            public bool HasIsTransient => IsTransient;
            public bool HasIsNull => IsNull;

            public MessageDescriptor Descriptor => throw new NotImplementedException("Simplified implementation");

            public int CalculateSize() => 0;
            public Metric Clone() => new Metric(this);
            public Metric() { }
            public Metric(Metric other) : this()
            {
                Name = other.Name;
                Alias = other.Alias;
                Timestamp = other.Timestamp;
                Datatype = other.Datatype;
                IsHistorical = other.IsHistorical;
                IsTransient = other.IsTransient;
                IsNull = other.IsNull;
                IntValue = other.IntValue;
                LongValue = other.LongValue;
                FloatValue = other.FloatValue;
                DoubleValue = other.DoubleValue;
                BooleanValue = other.BooleanValue;
                StringValue = other.StringValue;
                BytesValue = other.BytesValue;
                DatasetValue = other.DatasetValue?.Clone();
                TemplateValue = other.TemplateValue?.Clone();
            }

            public bool Equals(Metric? other) => ReferenceEquals(this, other);
            public override bool Equals(object? obj) => Equals(obj as Metric);
            public override int GetHashCode() => 0;

            public void MergeFrom(Metric other)
            {
                if (other.Name.Length != 0) Name = other.Name;
                if (other.Alias != 0UL) Alias = other.Alias;
                if (other.Timestamp != 0UL) Timestamp = other.Timestamp;
                if (other.Datatype != 0) Datatype = other.Datatype;
                if (other.IsHistorical) IsHistorical = other.IsHistorical;
                if (other.IsTransient) IsTransient = other.IsTransient;
                if (other.IsNull) IsNull = other.IsNull;
                if (other.IntValue != 0UL) IntValue = other.IntValue;
                if (other.LongValue != 0UL) LongValue = other.LongValue;
                if (other.FloatValue != 0F) FloatValue = other.FloatValue;
                if (other.DoubleValue != 0D) DoubleValue = other.DoubleValue;
                if (other.BooleanValue) BooleanValue = other.BooleanValue;
                if (other.StringValue.Length != 0) StringValue = other.StringValue;
                if (other.BytesValue != null) BytesValue = other.BytesValue;
                if (other.DatasetValue != null) DatasetValue = other.DatasetValue;
                if (other.TemplateValue != null) TemplateValue = other.TemplateValue;
            }

            public void MergeFrom(CodedInputStream input)
            {
                uint tag;
                while ((tag = input.ReadTag()) != 0)
                {
                    switch (tag)
                    {
                        case 10: Name = input.ReadString(); break;
                        case 16: Alias = input.ReadUInt64(); break;
                        case 24: Timestamp = input.ReadUInt64(); break;
                        case 32: Datatype = input.ReadUInt32(); break;
                        case 40: IsHistorical = input.ReadBool(); break;
                        case 48: IsTransient = input.ReadBool(); break;
                        case 56: IsNull = input.ReadBool(); break;
                        case 64: IntValue = input.ReadUInt64(); break;
                        case 72: LongValue = input.ReadUInt64(); break;
                        case 85: FloatValue = input.ReadFloat(); break;
                        case 89: DoubleValue = input.ReadDouble(); break;
                        case 96: BooleanValue = input.ReadBool(); break;
                        case 106: StringValue = input.ReadString(); break;
                        case 114: BytesValue = input.ReadBytes(); break;
                        default: input.SkipLastField(); break;
                    }
                }
            }

            public void WriteTo(CodedOutputStream output)
            {
                if (Name.Length != 0) { output.WriteRawTag(10); output.WriteString(Name); }
                if (Alias != 0UL) { output.WriteRawTag(16); output.WriteUInt64(Alias); }
                if (Timestamp != 0UL) { output.WriteRawTag(24); output.WriteUInt64(Timestamp); }
                if (Datatype != 0) { output.WriteRawTag(32); output.WriteUInt32(Datatype); }
                if (IsHistorical) { output.WriteRawTag(40); output.WriteBool(IsHistorical); }
                if (IsTransient) { output.WriteRawTag(48); output.WriteBool(IsTransient); }
                if (IsNull) { output.WriteRawTag(56); output.WriteBool(IsNull); }
                if (IntValue != 0UL) { output.WriteRawTag(64); output.WriteUInt64(IntValue); }
                if (LongValue != 0UL) { output.WriteRawTag(72); output.WriteUInt64(LongValue); }
                if (FloatValue != 0F) { output.WriteRawTag(85); output.WriteFloat(FloatValue); }
                if (DoubleValue != 0D) { output.WriteRawTag(89); output.WriteDouble(DoubleValue); }
                if (BooleanValue) { output.WriteRawTag(96); output.WriteBool(BooleanValue); }
                if (StringValue.Length != 0) { output.WriteRawTag(106); output.WriteString(StringValue); }
                if (BytesValue != null) { output.WriteRawTag(114); output.WriteBytes(BytesValue); }
            }
        }

        public sealed partial class DataSet : IMessage<DataSet>
        {
            private static readonly MessageParser<DataSet> _parser = new MessageParser<DataSet>(() => new DataSet());
            private readonly RepeatedField<string> columns_ = new RepeatedField<string>();
            private readonly RepeatedField<uint> types_ = new RepeatedField<uint>();
            private readonly RepeatedField<Types.Row> rows_ = new RepeatedField<Types.Row>();

            public static MessageParser<DataSet> Parser => _parser;

            public ulong NumOfColumns { get; set; }
            public RepeatedField<string> Columns => columns_;
            //public RepeatedField<uint> Types => types_;
            public RepeatedField<Types.Row> Rows => rows_;

            public MessageDescriptor Descriptor => throw new NotImplementedException();
            public int CalculateSize() => 0;
            public DataSet Clone() => new DataSet(this);
            public DataSet() { }
            public DataSet(DataSet other) : this()
            {
                NumOfColumns = other.NumOfColumns;
                columns_.Add(other.columns_);
                types_.Add(other.types_);
                rows_.Add(other.rows_);
            }

            public bool Equals(DataSet? other) => ReferenceEquals(this, other);
            public override bool Equals(object? obj) => Equals(obj as DataSet);
            public override int GetHashCode() => 0;
            public void MergeFrom(DataSet other) { }
            public void MergeFrom(CodedInputStream input) { }
            public void WriteTo(CodedOutputStream output) { }

            public static class Types
            {
                public sealed partial class Row : IMessage<Row>
                {
                    private static readonly MessageParser<Row> _parser = new MessageParser<Row>(() => new Row());
                    private readonly RepeatedField<DataSetValue> elements_ = new RepeatedField<DataSetValue>();

                    public static MessageParser<Row> Parser => _parser;
                    public RepeatedField<DataSetValue> Elements => elements_;

                    public MessageDescriptor Descriptor => throw new NotImplementedException();
                    public int CalculateSize() => 0;
                    public Row Clone() => new Row(this);
                    public Row() { }
                    public Row(Row other) : this() => elements_.Add(other.elements_);

                    public bool Equals(Row? other) => ReferenceEquals(this, other);
                    public override bool Equals(object? obj) => Equals(obj as Row);
                    public override int GetHashCode() => 0;
                    public void MergeFrom(Row other) { }
                    public void MergeFrom(CodedInputStream input) { }
                    public void WriteTo(CodedOutputStream output) { }
                }

                public sealed partial class DataSetValue : IMessage<DataSetValue>
                {
                    private static readonly MessageParser<DataSetValue> _parser = new MessageParser<DataSetValue>(() => new DataSetValue());

                    public static MessageParser<DataSetValue> Parser => _parser;

                    public ulong IntValue { get; set; }
                    public ulong LongValue { get; set; }
                    public float FloatValue { get; set; }
                    public double DoubleValue { get; set; }
                    public bool BooleanValue { get; set; }
                    public string StringValue { get; set; } = string.Empty;

                    public bool HasIntValue => IntValue != 0;
                    public bool HasLongValue => LongValue != 0;
                    public bool HasFloatValue => FloatValue != 0;
                    public bool HasDoubleValue => DoubleValue != 0;
                    public bool HasBooleanValue => BooleanValue;
                    public bool HasStringValue => !string.IsNullOrEmpty(StringValue);

                    public MessageDescriptor Descriptor => throw new NotImplementedException();
                    public int CalculateSize() => 0;
                    public DataSetValue Clone() => new DataSetValue(this);
                    public DataSetValue() { }
                    public DataSetValue(DataSetValue other) : this()
                    {
                        IntValue = other.IntValue;
                        LongValue = other.LongValue;
                        FloatValue = other.FloatValue;
                        DoubleValue = other.DoubleValue;
                        BooleanValue = other.BooleanValue;
                        StringValue = other.StringValue;
                    }

                    public bool Equals(DataSetValue? other) => ReferenceEquals(this, other);
                    public override bool Equals(object? obj) => Equals(obj as DataSetValue);
                    public override int GetHashCode() => 0;
                    public void MergeFrom(DataSetValue other) { }
                    public void MergeFrom(CodedInputStream input) { }
                    public void WriteTo(CodedOutputStream output) { }
                }
            }
        }

        public sealed partial class Template : IMessage<Template>
        {
            private static readonly MessageParser<Template> _parser = new MessageParser<Template>(() => new Template());
            private readonly RepeatedField<Metric> metrics_ = new RepeatedField<Metric>();
            private readonly RepeatedField<Types.Parameter> parameters_ = new RepeatedField<Types.Parameter>();

            public static MessageParser<Template> Parser => _parser;

            public string Version { get; set; } = string.Empty;
            public bool IsDefinition { get; set; }
            public RepeatedField<Metric> Metrics => metrics_;
            public RepeatedField<Types.Parameter> Parameters => parameters_;

            public MessageDescriptor Descriptor => throw new NotImplementedException();
            public int CalculateSize() => 0;
            public Template Clone() => new Template(this);
            public Template() { }
            public Template(Template other) : this()
            {
                Version = other.Version;
                IsDefinition = other.IsDefinition;
                metrics_.Add(other.metrics_);
                parameters_.Add(other.parameters_);
            }

            public bool Equals(Template? other) => ReferenceEquals(this, other);
            public override bool Equals(object? obj) => Equals(obj as Template);
            public override int GetHashCode() => 0;
            public void MergeFrom(Template other) { }
            public void MergeFrom(CodedInputStream input) { }
            public void WriteTo(CodedOutputStream output) { }

            public static class Types
            {
                public sealed partial class Parameter : IMessage<Parameter>
                {
                    private static readonly MessageParser<Parameter> _parser = new MessageParser<Parameter>(() => new Parameter());

                    public static MessageParser<Parameter> Parser => _parser;

                    public string Name { get; set; } = string.Empty;
                    public uint Type { get; set; }

                    public MessageDescriptor Descriptor => throw new NotImplementedException();
                    public int CalculateSize() => 0;
                    public Parameter Clone() => new Parameter(this);
                    public Parameter() { }
                    public Parameter(Parameter other) : this()
                    {
                        Name = other.Name;
                        Type = other.Type;
                    }

                    public bool Equals(Parameter? other) => ReferenceEquals(this, other);
                    public override bool Equals(object? obj) => Equals(obj as Parameter);
                    public override int GetHashCode() => 0;
                    public void MergeFrom(Parameter other) { }
                    public void MergeFrom(CodedInputStream input) { }
                    public void WriteTo(CodedOutputStream output) { }
                }
            }
        }
    }
}