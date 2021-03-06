﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using SmartHotel.IoT.Provisioning.Common.Models;
using SmartHotel.IoT.Provisioning.Common.Models.DigitalTwins;
using YamlDotNet.Serialization;

namespace SmartHotel.IoT.ProvisioningGenerator
{
	class Program
	{
		private const string ImageFolderRelativePath = "../Images";
		public static async Task<int> Main( string[] args ) => await CommandLineApplication.ExecuteAsync<Program>( args );

		[Option( "-op|--outputPrefix", Description = "Prefix of the output filenames" )]
		public string OutputFilePrefix { get; } = "SmartHotel";

		[Option( "-ddp|--definitionsDirectoryPath", Description = "Path to the directory containing .json definition files" )]
		public string DefinitionsDirectoryPath { get; } = Path.Combine( "SampleDefinitions", "MasterJson" );

		[Option( "-st|--subTenantName", Description = "Create a sub-Tenant with the given name" )]
		public string SubTenantName { get; }

		private async Task OnExecuteAsync()
		{
			//GenerateSampleDefinition(DefinitionFilename);

			GenerateProvisioningFiles();

			Console.WriteLine();
			Console.WriteLine();
			Console.WriteLine( "Press Enter to continue..." );
			Console.ReadLine();
		}

		private void GenerateProvisioningFiles()
		{
			if ( !Directory.Exists( DefinitionsDirectoryPath ) )
			{
				Console.WriteLine( $"Definitions directory not found: {DefinitionsDirectoryPath}" );
				return;
			}
			foreach ( string definitionFilePath in Directory.EnumerateFiles( DefinitionsDirectoryPath, "*.json" ) )
			{
				try
				{
					string siteJson = File.ReadAllText( definitionFilePath );
					var site = JsonConvert.DeserializeObject<Site>( siteJson );
					if ( site == null )
					{
						Console.WriteLine( $"Invalid definition file found in directory: {definitionFilePath}" );
						continue;
					}

					Console.WriteLine( $"Generating template from {definitionFilePath}" );

					if ( !Directory.Exists( site.OutputDirectory ) )
					{
						Directory.CreateDirectory( site.OutputDirectory );
					}

					if ( !GenerateSiteProvisioningFile( site.Brands, site.OutputDirectory ) )
					{
						Console.WriteLine( "Site provisioning file not written, skipping brand file generation." );
						return;
					}

					int globalHotelNumber = 0;
					for ( int i = 0; i < site.Brands.Count; i++ )
					{
						Brand brand = site.Brands[i];
						GenerateBrandProvisioningFile( brand, i + 1, site.HotelTypes, site.OutputDirectory, ref globalHotelNumber );
					}

					Console.WriteLine();
				}
				catch ( Exception ex )
				{
					Console.WriteLine( $"Error occured processing definition file ({definitionFilePath}) - {ex}" );
				}
			}
		}

		private bool GenerateSiteProvisioningFile( List<Brand> brands, string outputDirectory )
		{
			string siteFilename = Path.Combine( outputDirectory, $"{OutputFilePrefix}_Site_Provisioning.yaml" );

			if ( File.Exists( siteFilename ) )
			{
				var overwrite = Prompt.GetYesNo( "A site provisioning file already exists," +
												" would you like to overwrite it and any brand provisioning" +
												 $" files that already exist? ({siteFilename})", false );
				if ( !overwrite )
				{
					return false;
				}
			}

			var p = new ProvisioningDescription();
			p.AddEndpoint( new EndpointDescription { type = "EventHub", eventTypes = new List<string> { "DeviceMessage" } } );
			var tenantSpace = new SpaceDescription
			{
				name = "SmartHotel 360 Tenant",
				description = "This is the root node for the SmartHotel360 IoT Demo",
				friendlyName = "SmartHotel 360 Tenant",
				type = "Tenant",
				keystoreName = "SmartHotel360 Keystore"
			};
			SpaceDescription desiredTenantSpace = tenantSpace;
			p.AddSpace( tenantSpace );

			if ( !string.IsNullOrWhiteSpace( SubTenantName ) )
			{
				var subtenantSpace = new SpaceDescription
				{
					name = SubTenantName,
					description = $"This is the root node for the {SubTenantName} sub Tenant",
					friendlyName = SubTenantName,
					type = "Tenant"
				};
				tenantSpace.AddSpace( subtenantSpace );
				desiredTenantSpace = subtenantSpace;
			}
			else
			{
				tenantSpace.AddResource( new ResourceDescription { type = "IoTHub" } );
			}

			desiredTenantSpace.AddType( new TypeDescription { name = "Classic", category = "SensorType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = "HotelBrand", category = "SpaceType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = "Hotel", category = "SpaceType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = "VIPFloor", category = "SpaceSubType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = "QueenRoom", category = "SpaceSubType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = "KingRoom", category = "SpaceSubType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = "SuiteRoom", category = "SpaceSubType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = "VIPSuiteRoom", category = "SpaceSubType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = "ConferenceRoom", category = "SpaceSubType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = "GymRoom", category = "SpaceSubType" } );
			desiredTenantSpace.AddType( new TypeDescription { name = BlobDescription.FloorplanFileBlobSubType, category = "SpaceBlobSubType" } );

			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.DeviceIdPrefixName,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.String,
				description = "Prefix used in sending Device Method calls to the IoT Hub."
			} );
			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.DisplayOrder,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.UInt,
				description = "Order to display spaces"
			} );
			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.MinTemperatureAlertThreshold,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.Int,
				description = "Alert if the temperature goes below this value."
			} );
			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.MaxTemperatureAlertThreshold,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.Int,
				description = "Alert if the temperature goes above this value."
			} );
			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.ImagePath,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.String,
				description = "Path of the image to display for the space."
			} );
			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.ImageBlobId,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.String,
				description = "Id of the image blob for the space."
			} );
			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.DetailedImagePath,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.String,
				description = "Path of the detailed image to display for the space."
			} );
			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.DetailedImageBlobId,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.String,
				description = "Id of the detailed image blob for the space."
			} );
			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.Latitude,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.String,
				description = "Geo Position"
			} );
			desiredTenantSpace.AddPropertyKey( new PropertyKeyDescription
			{
				name = PropertyKeyDescription.Longitude,
				primitiveDataType = PropertyKeyDescription.PrimitiveDataType.String,
				description = "Geo Position"
			} );

			desiredTenantSpace.AddUser( "Head Of Operations" );

			var matcherTemperature = new MatcherDescription { name = "Matcher Temperature", dataTypeValue = "Temperature" };
			desiredTenantSpace.AddMatcher( matcherTemperature );
			var temperatureProcessor = new UserDefinedFunctionDescription
			{
				name = "Temperature Processor",
				matcherNames = new List<string>(),
				script = "../UserDefinedFunctions/temperatureThresholdAlert.js"
			};
			temperatureProcessor.matcherNames.Add( matcherTemperature.name );
			desiredTenantSpace.AddUserDefinedFunction( temperatureProcessor );

			desiredTenantSpace.AddRoleAssignment( new RoleAssignmentDescription
			{
				roleId = RoleAssignment.RoleIds.SpaceAdmin,
				objectName = temperatureProcessor.name,
				objectIdType = RoleAssignment.ObjectIdTypes.UserDefinedFunctionId
			} );

			foreach ( Brand brand in brands )
			{
				string brandFilename = GetBrandProvisioningFilename( brand );
				desiredTenantSpace.AddSpaceReference( new SpaceReferenceDescription { filename = brandFilename } );
			}

			var yamlSerializer = new Serializer();
			string serializedProvisioningDescription = yamlSerializer.Serialize( p );
			File.WriteAllText( siteFilename, serializedProvisioningDescription );

			Console.WriteLine( $"Successfully created site provisioning file: {siteFilename}" );

			return true;
		}

		private string GetBrandProvisioningFilename( Brand brand )
		{
			return $"{OutputFilePrefix}_{brand.Name}_Provisioning.yaml";
		}

		private void GenerateBrandProvisioningFile( Brand brand, int brandNumber, List<HotelType> hotelTypes,
			string outputDirectory, ref int globalHotelNumber )
		{
			string brandFilename = Path.Combine( outputDirectory, GetBrandProvisioningFilename( brand ) );

			var brandSpaceDescription = new SpaceDescription
			{
				name = brand.Name,
				description = $"SmartHotel360 {brand.Name}",
				friendlyName = brand.Name,
				type = "HotelBrand"
			};
			brandSpaceDescription.AddUser( $"Hotel Brand {brandNumber} Manager" );
			brandSpaceDescription.AddProperty( new PropertyDescription { name = PropertyKeyDescription.DisplayOrder, value = brandNumber.ToString() } );

			brandSpaceDescription.AddBlob( new BlobDescription
			{
				name = $"{brand.Name} Blob",
				type = BlobDescription.FileBlobType,
				subtype = BlobDescription.NoneBlobType,
				description = "Brand image",
				filepath = $"{ImageFolderRelativePath}/brands/brand{brandNumber}.png",
				contentType = BlobDescription.PngContentType,
				isPrimaryBlob = true
			} );

			// Create the hotels
			for ( int hotelIndex = 0; hotelIndex < brand.Hotels.Count; hotelIndex++ )
			{
				globalHotelNumber++;
				Hotel hotel = brand.Hotels[hotelIndex];
				HotelType hotelType = hotelTypes.First( t => t.Name == hotel.Type );
				var hotelSpaceDescription = new SpaceDescription
				{
					name = hotel.Name,
					description = $"SmartHotel360 {hotel.Name}",
					friendlyName = hotel.Name,
					type = "Hotel"
				};
				hotelSpaceDescription.AddUser( $"Hotel {globalHotelNumber} Manager" );

				hotelSpaceDescription.AddProperty( new PropertyDescription
				{ name = PropertyKeyDescription.DisplayOrder, value = hotelIndex.ToString() } );

				hotelSpaceDescription.AddProperty( new PropertyDescription
				{ name = PropertyKeyDescription.MinTemperatureAlertThreshold, value = hotelType.MinTempAlertThreshold.ToString() } );

				hotelSpaceDescription.AddProperty( new PropertyDescription
				{ name = PropertyKeyDescription.MaxTemperatureAlertThreshold, value = hotelType.MaxTempAlertThreshold.ToString() } );

				hotelSpaceDescription.AddProperty( new PropertyDescription
				{ name = PropertyKeyDescription.Latitude, value = hotel.Latitude.ToString() } );

				hotelSpaceDescription.AddProperty( new PropertyDescription
				{ name = PropertyKeyDescription.Longitude, value = hotel.Longitude.ToString() } );

				hotelSpaceDescription.AddBlob( new BlobDescription
				{
					name = $"{brand.Name} {hotel.Name} Blob",
					type = BlobDescription.FileBlobType,
					subtype = BlobDescription.NoneBlobType,
					description = "Hotel image",
					filepath = $"{ImageFolderRelativePath}/hotels/{hotelType.Name.ToLower()}.jpg",
					contentType = BlobDescription.JpegContentType,
					isPrimaryBlob = true
				} );

				string brandHotelPrefix = $"{brand.Name}-{hotel.Name}-".Replace( " ", string.Empty );

				int numberRegularFloors = hotelType.TotalNumberFloors - hotelType.NumberVipFloors;

				// Create the floors
				for ( int floorIndex = 0; floorIndex < hotelType.TotalNumberFloors; floorIndex++ )
				{
					bool isVipFloor = floorIndex >= numberRegularFloors;
					string floorName = $"Floor {floorIndex + 1:D02}";
					var floorSpaceDescription = new SpaceDescription
					{
						name = floorName,
						description = $"Floor {floorIndex + 1}",
						friendlyName = $"Floor {floorIndex + 1}",
						type = "Floor"
					};
					floorSpaceDescription.AddProperty( new PropertyDescription
					{
						name = PropertyKeyDescription.DeviceIdPrefixName,
						value = brandHotelPrefix
					} );

					string imagePathSuffix = string.Empty;
					if ( isVipFloor )
					{
						imagePathSuffix = "vip";
						floorSpaceDescription.subType = "VIPFloor";
					}

					floorSpaceDescription.AddBlob( new BlobDescription
					{
						name = $"{brand.Name} {hotel.Name} {floorName} Blob",
						type = BlobDescription.FileBlobType,
						subtype = BlobDescription.NoneBlobType,
						description = "Floor image",
						filepath = $"{ImageFolderRelativePath}/floors/{hotelType.Name.ToLower()}{imagePathSuffix}.jpg",
						contentType = BlobDescription.JpegContentType,
						isPrimaryBlob = true
					} );

					floorSpaceDescription.AddBlob( new BlobDescription
					{
						name = $"{brand.Name} {hotel.Name} {floorName} Floorplan Blob",
						type = BlobDescription.FileBlobType,
						subtype = BlobDescription.FloorplanFileBlobSubType,
						description = "Floorplan image",
						filepath = $"{ImageFolderRelativePath}/floorplans/{hotelType.Name.ToLower()}{imagePathSuffix}.svg",
						contentType = BlobDescription.SvgContentType,
						isPrimaryBlob = false
					} );

					if ( !isVipFloor && !string.IsNullOrEmpty( hotel.RegularFloorEmployeeUser ) )
					{
						floorSpaceDescription.AddUser( $"Hotel {hotelIndex + 1} {hotel.RegularFloorEmployeeUser}" );
					}

					bool includeGymForThisFloor = floorIndex == 0 && hotelType.IncludeGym;
					bool includeConferenceRoomForThisFloor = floorIndex == 1 && hotelType.IncludeConferenceRoom;

					int numberOfRooms = isVipFloor ? hotelType.NumberRoomsPerVipFloor : hotelType.NumberRoomsPerRegularFloor;
					if ( includeGymForThisFloor )
					{
						numberOfRooms--;
					}
					if ( includeConferenceRoomForThisFloor )
					{
						numberOfRooms--;
					}

					// Create the rooms
					for ( int roomIndex = 0; roomIndex < numberOfRooms; roomIndex++ )
					{
						string roomType = GetRoomType( roomIndex, numberOfRooms, isVipFloor );
						SpaceDescription roomSpaceDescription = CreateRoom( 100 * ( floorIndex + 1 ) + roomIndex + 1, brandHotelPrefix,
							roomType, hotel.AddDevices );
						floorSpaceDescription.AddSpace( roomSpaceDescription );
					}

					if ( includeGymForThisFloor )
					{
						SpaceDescription roomSpaceDescription = CreateRoom( 100 * ( floorIndex + 1 ) + numberOfRooms + 1,
							brandHotelPrefix, "GymRoom", hotel.AddDevices );
						floorSpaceDescription.AddSpace( roomSpaceDescription );
					}

					if ( includeConferenceRoomForThisFloor )
					{
						SpaceDescription roomSpaceDescription = CreateRoom( 100 * ( floorIndex + 1 ) + numberOfRooms + 1,
							brandHotelPrefix, "ConferenceRoom", hotel.AddDevices );
						floorSpaceDescription.AddSpace( roomSpaceDescription );
					}

					hotelSpaceDescription.AddSpace( floorSpaceDescription );
				}

				brandSpaceDescription.AddSpace( hotelSpaceDescription );
			}

			var yamlSerializer = new SerializerBuilder()
				.Build();
			string serializedProvisioningDescription = yamlSerializer.Serialize( brandSpaceDescription );
			File.WriteAllText( brandFilename, serializedProvisioningDescription );

			Console.WriteLine( $"Successfully created brand provisioning file: {brandFilename}" );
		}

		private string GetRoomType( int roomIndex, int numberRoomsOnFloor, bool isVipFloor )
		{
			if ( isVipFloor )
			{
				int oneQuarter = (int)Math.Ceiling( (double)numberRoomsOnFloor / 4.0D );
				if ( roomIndex < oneQuarter )
				{
					return "SuiteRoom";
				}
				else
				{
					return "VIPSuiteRoom";
				}
			}
			else
			{
				int oneThird = (int)Math.Ceiling( (double)numberRoomsOnFloor / 3.0D );
				if ( roomIndex < oneThird )
				{
					return "QueenRoom";
				}
				else if ( roomIndex < 2 * oneThird )
				{
					return "KingRoom";
				}
				else
				{
					return "SuiteRoom";
				}
			}
		}

		private SpaceDescription CreateRoom( int roomNumber, string brandHotelPrefix, string roomType, bool addDevices )
		{
			var roomSpaceDescription = new SpaceDescription
			{
				name = $"Room {roomNumber}",
				description = $"Room {roomNumber}",
				friendlyName = $"Room {roomNumber}",
				type = "Room",
				subType = roomType
			};

			if ( addDevices )
			{
				var roomDevice = new DeviceDescription
				{
					name = "Room",
					hardwareId = $"{brandHotelPrefix}{roomNumber}"
				};
				roomDevice.AddSensor( new SensorDescription { dataType = "Temperature", type = "Classic" } );
				roomDevice.AddSensor( new SensorDescription { dataType = "Motion", type = "Classic" } );
				roomDevice.AddSensor( new SensorDescription { dataType = "Light", type = "Classic" } );

				roomSpaceDescription.AddDevice( roomDevice );
			}

			return roomSpaceDescription;
		}

		// This is NOT up to date with the latest definition files, just how they were created in the first place
		//private void GenerateSampleDefinition( string definitionFilename )
		//{
		//	var hotelTypeH = new HotelType
		//	{
		//		Name = "H",
		//		TotalNumberFloors = 10,
		//		NumberVipFloors = 2,
		//		NumberRoomsPerRegularFloor = 20,
		//		NumberRoomsPerVipFloor = 10,
		//		IncludeConferenceRoom = true,
		//		IncludeGym = true
		//	};

		//	var hotelTypeL = new HotelType
		//	{
		//		Name = "L",
		//		TotalNumberFloors = 10,
		//		NumberVipFloors = 2,
		//		NumberRoomsPerRegularFloor = 15,
		//		NumberRoomsPerVipFloor = 8,
		//		IncludeConferenceRoom = true,
		//		IncludeGym = true
		//	};

		//	var hotelTypeSH = new HotelType
		//	{
		//		Name = "SH",
		//		TotalNumberFloors = 5,
		//		NumberVipFloors = 1,
		//		NumberRoomsPerRegularFloor = 10,
		//		NumberRoomsPerVipFloor = 4,
		//		IncludeConferenceRoom = true,
		//		IncludeGym = true
		//	};

		//	var hotelTypeSL = new HotelType
		//	{
		//		Name = "SL",
		//		TotalNumberFloors = 5,
		//		NumberVipFloors = 1,
		//		NumberRoomsPerRegularFloor = 10,
		//		NumberRoomsPerVipFloor = 4,
		//		IncludeConferenceRoom = true,
		//		IncludeGym = true
		//	};

		//	var hotelTypes = new List<HotelType> { hotelTypeH, hotelTypeL, hotelTypeSH, hotelTypeSL };

		//	var brands = new List<Brand>();

		//	for ( int i = 0; i < 4; i++ )
		//	{
		//		var hotels = new List<Hotel>();

		//		switch ( i )
		//		{
		//			case 0:
		//				{
		//					hotels.Add( CreateHotel( hotelTypeH, 1, "Employee", true ) );
		//					hotels.Add( CreateHotel( hotelTypeL, 1, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSH, 1, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSH, 2, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSL, 1, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSL, 2, null, AllDevices ) );
		//					break;
		//				}
		//			case 1:
		//				{
		//					hotels.Add( CreateHotel( hotelTypeSH, 1, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSH, 2, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSL, 1, null, AllDevices ) );
		//					break;
		//				}
		//			case 2:
		//				{
		//					hotels.Add( CreateHotel( hotelTypeSH, 1, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSL, 1, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSL, 2, null, AllDevices ) );
		//					break;
		//				}
		//			case 3:
		//				{
		//					hotels.Add( CreateHotel( hotelTypeSH, 1, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSH, 2, null, AllDevices ) );
		//					hotels.Add( CreateHotel( hotelTypeSL, 1, null, AllDevices ) );
		//					break;
		//				}
		//		}

		//		var brand = new Brand
		//		{
		//			Name = $"Brand {i + 1}",
		//			Hotels = hotels
		//		};

		//		brands.Add( brand );
		//	}

		//	var site = new Site
		//	{
		//		HotelTypes = hotelTypes,
		//		Brands = brands
		//	};

		//	string siteJson = JsonConvert.SerializeObject( site, Formatting.Indented );
		//	using ( StreamWriter definitionFile = new StreamWriter( definitionFilename ) )
		//	{
		//		definitionFile.Write( siteJson );
		//	}
		//}

		private Hotel CreateHotel( HotelType hotelType, int hotelIndex, string regularFloorEmployeeUser, bool addDevices )
		{
			return new Hotel
			{
				Name = $"Hotel {hotelType.Name} {hotelIndex}",
				Type = hotelType.Name,
				RegularFloorEmployeeUser = regularFloorEmployeeUser,
				AddDevices = addDevices
			};
		}
	}
}
