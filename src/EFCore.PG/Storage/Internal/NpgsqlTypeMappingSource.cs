#region License
// The PostgreSQL License
//
// Copyright (C) 2016 The Npgsql Development Team
//
// Permission to use, copy, modify, and distribute this software and its
// documentation for any purpose, without fee, and without a written
// agreement is hereby granted, provided that the above copyright notice
// and this paragraph and the following two paragraphs appear in all copies.
//
// IN NO EVENT SHALL THE NPGSQL DEVELOPMENT TEAM BE LIABLE TO ANY PARTY
// FOR DIRECT, INDIRECT, SPECIAL, INCIDENTAL, OR CONSEQUENTIAL DAMAGES,
// INCLUDING LOST PROFITS, ARISING OUT OF THE USE OF THIS SOFTWARE AND ITS
// DOCUMENTATION, EVEN IF THE NPGSQL DEVELOPMENT TEAM HAS BEEN ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.
//
// THE NPGSQL DEVELOPMENT TEAM SPECIFICALLY DISCLAIMS ANY WARRANTIES,
// INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY
// AND FITNESS FOR A PARTICULAR PURPOSE. THE SOFTWARE PROVIDED HEREUNDER IS
// ON AN "AS IS" BASIS, AND THE NPGSQL DEVELOPMENT TEAM HAS NO OBLIGATIONS
// TO PROVIDE MAINTENANCE, SUPPORT, UPDATES, ENHANCEMENTS, OR MODIFICATIONS.
#endregion

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage.Internal.Mapping;
using Npgsql;
using NpgsqlTypes;

namespace Microsoft.EntityFrameworkCore.Storage.Internal
{
    public class NpgsqlTypeMappingSource : RelationalTypeMappingSource
    {
        readonly ConcurrentDictionary<string, RelationalTypeMapping> _storeTypeMappings;
        readonly ConcurrentDictionary<Type, RelationalTypeMapping> _clrTypeMappings;

        static readonly string[] SizableStoreTypes =
        {
            "character varying", "varchar",
            "character", "char",
            "bit", "bit varying"
        };

        #region Mappings

        readonly NpgsqlBoolTypeMapping      _bool      = new NpgsqlBoolTypeMapping();
        readonly NpgsqlByteArrayTypeMapping _bytea     = new NpgsqlByteArrayTypeMapping();
        readonly FloatTypeMapping           _float4    = new FloatTypeMapping("real", DbType.Single);
        readonly DoubleTypeMapping          _float8    = new DoubleTypeMapping("double precision", DbType.Double);
        readonly DecimalTypeMapping         _numeric   = new DecimalTypeMapping("numeric", DbType.Decimal);
        readonly DecimalTypeMapping         _money     = new DecimalTypeMapping("money");
        readonly GuidTypeMapping            _uuid      = new GuidTypeMapping("uuid", DbType.Guid);
        readonly ShortTypeMapping           _int2      = new ShortTypeMapping("smallint", DbType.Int16);
        readonly IntTypeMapping             _int4      = new IntTypeMapping("integer", DbType.Int32);
        readonly LongTypeMapping            _int8      = new LongTypeMapping("bigint", DbType.Int64);
        readonly StringTypeMapping          _text      = new StringTypeMapping("text", DbType.String);
        readonly StringTypeMapping          _varchar   = new StringTypeMapping("character varying", DbType.String);
        readonly StringTypeMapping          _char      = new StringTypeMapping("character", DbType.String);
        readonly NpgsqlJsonbTypeMapping     _jsonb     = new NpgsqlJsonbTypeMapping();
        readonly NpgsqlJsonTypeMapping      _json      = new NpgsqlJsonTypeMapping();
        readonly DateTimeTypeMapping        _timestamp = new DateTimeTypeMapping("timestamp without time zone", DbType.DateTime);
        // TODO: timestamptz
        readonly NpgsqlIntervalTypeMapping  _interval  = new NpgsqlIntervalTypeMapping();
        // TODO: time
        readonly NpgsqlTimeTzTypeMapping    _timetz    = new NpgsqlTimeTzTypeMapping();
        readonly NpgsqlMacaddrTypeMapping   _macaddr   = new NpgsqlMacaddrTypeMapping();
        readonly NpgsqlInetTypeMapping      _inet      = new NpgsqlInetTypeMapping();
        readonly NpgsqlBitTypeMapping       _bit       = new NpgsqlBitTypeMapping();
        readonly NpgsqlVarbitTypeMapping    _varbit    = new NpgsqlVarbitTypeMapping();
        readonly NpgsqlHstoreTypeMapping    _hstore    = new NpgsqlHstoreTypeMapping();
        readonly NpgsqlPointTypeMapping     _point     = new NpgsqlPointTypeMapping();
        readonly NpgsqlLineTypeMapping      _line      = new NpgsqlLineTypeMapping();
        readonly NpgsqlXidTypeMapping       _xid       = new NpgsqlXidTypeMapping();
        readonly NpgsqlOidTypeMapping       _oid       = new NpgsqlOidTypeMapping();
        readonly NpgsqlCidTypeMapping       _cid       = new NpgsqlCidTypeMapping();
        readonly NpgsqlRegtypeTypeMapping   _regtype   = new NpgsqlRegtypeTypeMapping();

        #endregion Mappings

        public NpgsqlTypeMappingSource([NotNull] TypeMappingSourceDependencies dependencies,
            [NotNull] RelationalTypeMappingSourceDependencies relationalDependencies)
            : base(dependencies, relationalDependencies)
        {
            // Note that PostgreSQL has aliases to some built-in types (e.g. int4 for integer),
            // these are mapped as well.
            // https://www.postgresql.org/docs/9.5/static/datatype.html#DATATYPE-TABLE
            var storeTypeMappings = new Dictionary<string, RelationalTypeMapping>(StringComparer.OrdinalIgnoreCase)
            {
                { "boolean",                     _bool      },
                { "bool",                        _bool      },
                { "bytea",                       _bytea     },
                { "real",                        _float4    },
                { "float4",                      _float4    },
                { "double precision",            _float8    },
                { "float8",                      _float8    },
                { "numeric",                     _numeric   },
                { "decimal",                     _numeric   },
                { "money",                       _money     },
                { "uuid",                        _uuid      },
                { "smallint",                    _int2      },
                { "int2",                        _int2      },
                { "integer",                     _int4      },
                { "int",                         _int4      },
                { "int4",                        _int4      },
                { "bigint",                      _int8      },
                { "int8",                        _int8      },
                { "text",                        _text      },
                { "jsonb",                       _jsonb     },
                { "json",                        _json      },
                { "character varying",           _varchar   },
                { "varchar",                     _varchar   },
                { "character",                   _char      },
                { "char",                        _char      },
                { "timestamp without time zone", _timestamp },
                { "timestamp",                   _timestamp },
                { "interval",                    _interval  },
                { "time with time zone",         _timetz    },
                { "timetz",                      _timetz    },
                { "macaddr",                     _macaddr   },
                { "inet",                        _inet      },
                { "bit",                         _bit       },
                { "bit varying",                 _varbit    },
                { "varbit",                      _varbit    },
                { "hstore",                      _hstore    },
                { "point",                       _point     },
                { "line",                        _line      },
                { "xid",                         _xid       },
                { "oid",                         _oid       },
                { "cid",                         _cid       },
                { "regtype",                     _regtype   },
            };

            var clrTypeMappings = new Dictionary<Type, RelationalTypeMapping>
            {
                { typeof(bool),                       _bool      },
                { typeof(byte[]),                     _bytea     },
                { typeof(float),                      _float4    },
                { typeof(double),                     _float8    },
                { typeof(decimal),                    _numeric   },
                { typeof(Guid),                       _uuid      },
                { typeof(short),                      _int2      },
                { typeof(int),                        _int4      },
                { typeof(long),                       _int8      },
                { typeof(string),                     _text      },
                { typeof(DateTime),                   _timestamp },
                { typeof(TimeSpan),                   _interval  },
                { typeof(PhysicalAddress),            _macaddr   },
                { typeof(IPAddress),                  _inet      },
                { typeof(BitArray),                   _varbit    },
                { typeof(Dictionary<string, string>), _hstore    },
                { typeof(NpgsqlPoint),                _point     },
                { typeof(NpgsqlLine),                 _line      },
            };

            _storeTypeMappings = new ConcurrentDictionary<string, RelationalTypeMapping>(storeTypeMappings);
            _clrTypeMappings = new ConcurrentDictionary<Type, RelationalTypeMapping>(clrTypeMappings);
        }

        protected override RelationalTypeMapping FindMapping(RelationalTypeMappingInfo mappingInfo)
        {
            RelationalTypeMapping mapping;

            var storeType = mappingInfo.StoreTypeName;
            if (storeType != null)
            {
                if (!_storeTypeMappings.TryGetValue(mappingInfo.StoreTypeName, out mapping))
                    mapping = FindSizableMapping(storeType);
                if (mapping != null)
                    return mapping;
            }

            var clrType = mappingInfo.ProviderClrType;
            if (clrType == null)
            {
                //Log.Warn($"Received RelationalTypeMappingInfo without {mappingInfo.StoreTypeName} or {mappingInfo.TargetClrType}");
                return null;
            }

            // TODO: Cache sized mappings?
            if (mappingInfo.Size.HasValue)
            {
                if (clrType == typeof(string))
                    return _varchar.Clone($"varchar({mappingInfo.Size})", mappingInfo.Size);
                if (clrType == typeof(BitArray))
                    return _varbit.Clone($"varbit({mappingInfo.Size})", mappingInfo.Size);
            }

            if (_clrTypeMappings.TryGetValue(clrType, out mapping))
                return mapping;

            mapping = FindArrayMapping(mappingInfo);
            return mapping;

            // TODO: range, enum, composite
        }

        RelationalTypeMapping FindArrayMapping(RelationalTypeMappingInfo mappingInfo)
        {
            // PostgreSQL array types prefix the element type with underscore
            var storeType = mappingInfo.StoreTypeName;
            if (storeType != null && storeType.StartsWith("_"))
            {
                var elementMapping = FindMapping(storeType.Substring(1));
                if (elementMapping != null)
                    return _storeTypeMappings.GetOrAdd(storeType, new NpgsqlArrayTypeMapping(elementMapping));
            }

            var clrType = mappingInfo.ProviderClrType;
            if (clrType == null)
                return null;

            // Try to see if it is an array type
            var arrayElementType = GetArrayElementType(clrType);
            if (arrayElementType != null)
            {
                var elementMapping = (RelationalTypeMapping)FindMapping(arrayElementType);

                // If an element isn't supported, neither is its array
                if (elementMapping == null)
                    return null;

                // Arrays of arrays aren't supported (as opposed to multidimensional arrays) by PostgreSQL
                if (elementMapping is NpgsqlArrayTypeMapping)
                    return null;

                return _clrTypeMappings.GetOrAdd(clrType, new NpgsqlArrayTypeMapping(elementMapping, clrType));
            }

            return null;
        }

        RelationalTypeMapping FindSizableMapping(string storeType)
        {
            var openParen = storeType.IndexOf("(", StringComparison.Ordinal);
            if (openParen <= 0)
                return null;

            var baseStoreType = storeType.Substring(0, openParen).ToLower();

            if (!SizableStoreTypes.Contains(baseStoreType))
                return null;

            // TODO: Shouldn't happen, at least warn
            if (!_storeTypeMappings.TryGetValue(baseStoreType, out var mapping))
            {
                Debug.Fail($"Type is in {nameof(SizableStoreTypes)} but wasn't found in {nameof(_storeTypeMappings)}");
                return null;
            }

            var closeParen = storeType.IndexOf(")", openParen + 1, StringComparison.Ordinal);

            // TODO: Cache sized mappings?
            if (closeParen > openParen
                && int.TryParse(storeType.Substring(openParen + 1, closeParen - openParen - 1), out var size))
            {
                return mapping.Clone(storeType, size);
            }

            return null;
        }

        [CanBeNull]
        static Type GetArrayElementType(Type type)
        {
            var typeInfo = type.GetTypeInfo();
            if (typeInfo.IsArray)
                return type.GetElementType();

            var ilist = typeInfo.ImplementedInterfaces.FirstOrDefault(x => x.GetTypeInfo().IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>));
            return ilist != null ? ilist.GetGenericArguments()[0] : null;
        }
    }
}
