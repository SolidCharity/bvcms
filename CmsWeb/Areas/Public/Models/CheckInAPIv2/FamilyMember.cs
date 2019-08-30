﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CmsData;
using CmsData.Classes.DataMapper;
using ImageData;

namespace CmsWeb.Areas.Public.Models.CheckInAPIv2
{
	[SuppressMessage( "ReSharper", "CollectionNeverQueried.Global" )]
	[SuppressMessage( "ReSharper", "UnusedMember.Global" )]
	[SuppressMessage( "ReSharper", "NotAccessedField.Global" )]
	[SuppressMessage( "ReSharper", "MemberCanBePrivate.Global" )]
	public class FamilyMember : DataMapper
	{
		public int id = 0;
		public int age = 0;
		public DateTime birthday = DateTime.MinValue;
		public int position = 0;
		public int genderID = 0;

		public string name = "";
		public string altName = "";
		public string email = "";
		public string mobile = "";
		public string picture = "";

		public int pictureX = 0;
		public int pictureY = 0;

		public List<Group> groups = new List<Group>();

		public static List<FamilyMember> forFamilyID(CMSDataContext cmsdb, CMSImageDataContext cmsidb, int familyID, int campus, DateTime date, bool returnPictureUrls)
		{
			List<FamilyMember> members = new List<FamilyMember>();
			DataTable table = new DataTable();

			string qMembers = @"SELECT *
										FROM (SELECT
													person.PeopleId AS id,
													person.Name AS name,
													ISNULL( person.Age, 0 ) AS age,
													person.BDate AS birthday,
													gender.Id AS genderID,
													EmailAddress AS email,
													CellPhone AS mobile
												FROM dbo.People AS person
													LEFT JOIN lookup.Gender AS gender ON person.GenderId = gender.Id
												WHERE person.FamilyId = @familyID
												UNION
												SELECT
													person.PeopleId AS id,
													person.Name AS name,
													ISNULL( person.Age, 0 ) AS age,
													person.BDate AS birthday,
													gender.Id AS genderID,
													EmailAddress AS email,
													CellPhone AS mobile
												FROM dbo.PeopleExtra AS extra
													INNER JOIN People AS person on person.PeopleId = extra.PeopleId
													LEFT JOIN lookup.Gender AS gender ON person.GenderId = gender.Id
												WHERE extra.Field = 'Parent'
														AND extra.IntValue IN (SELECT person.PeopleId
																					  FROM dbo.People AS person
																					  WHERE person.FamilyId = @familyID)
											  ) AS familyMembers
										ORDER BY familyMembers.Age DESC, familyMembers.genderID";

			using( SqlCommand cmd = new SqlCommand( qMembers, cmsdb.ReadonlyConnection() as SqlConnection) ) {
				SqlParameter parameter = new SqlParameter( "familyID", familyID );

				cmd.Parameters.Add( parameter );

				SqlDataAdapter adapter = new SqlDataAdapter( cmd );
				adapter.Fill( table );
			}

			foreach( DataRow row in table.Rows ) {
				FamilyMember member = new FamilyMember();
				member.populate( row );
				member.loadPicture(cmsdb, cmsidb, returnPictureUrls);
				member.loadGroups(cmsdb, campus, date);

				members.Add( member );
			}

			return members;
		}

		private void loadGroups(CMSDataContext db, int campus, DateTime date )
		{
			groups.AddRange( Group.forPersonID( db, id, campus, date ) );
		}

		private void loadPicture(CMSDataContext cmsdb, CMSImageDataContext cmsidb, bool returnUrl)
		{
			var person = cmsdb.People.SingleOrDefault(p => p.PeopleId == id);
            int? ImageId;

            if (person == null || person.Picture == null)
            {
                if (returnUrl)
                {
                    var pic = new CmsData.Picture();
                    picture = pic.MediumUrl;
                    return;
                }
                ImageId = CmsData.Picture.SmallMissingGenericId;
            }
            else
            {
                pictureX = person.Picture.X.GetValueOrDefault();
                pictureY = person.Picture.Y.GetValueOrDefault();

                if (returnUrl)
                {
                    switch (person?.Gender?.Code)
                    {
                        case "M":
                            picture = person.Picture.MediumMaleUrl;
                            break;
                        case "F":
                            picture = person.Picture.MediumFemaleUrl;
                            break;
                        default:
                            picture = person.Picture.MediumUrl;
                            break;
                    }
                    return;
                }
                ImageId = person.Picture.SmallId;
            }

			Image image = cmsidb.Images.SingleOrDefault( i => i.Id == ImageId );

			if( image != null ) {
				picture = Convert.ToBase64String( image.Bits );
			}
		}
	}
}
