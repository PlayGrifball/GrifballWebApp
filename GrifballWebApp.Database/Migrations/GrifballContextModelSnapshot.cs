﻿// <auto-generated />
using System;
using GrifballWebApp.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace GrifballWebApp.Database.Migrations
{
    [DbContext(typeof(GrifballContext))]
    partial class GrifballContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.1")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("GrifballWebApp.Database.Models.Match", b =>
                {
                    b.Property<Guid>("MatchID")
                        .HasColumnType("uniqueidentifier");

                    b.Property<DateTime?>("EndTime")
                        .HasColumnType("datetime2");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<DateTime?>("StartTime")
                        .HasColumnType("datetime2");

                    b.HasKey("MatchID");

                    b.ToTable("Matches", "Infinite");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("MatchesHistory", "Infinite");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MatchLink", b =>
                {
                    b.Property<Guid>("MatchID")
                        .HasColumnType("uniqueidentifier");

                    b.Property<int>("SeasonMatchID")
                        .HasColumnType("int");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.HasKey("MatchID", "SeasonMatchID");

                    b.HasIndex("MatchID")
                        .IsUnique();

                    b.HasIndex("SeasonMatchID")
                        .IsUnique();

                    b.ToTable("MatchLinks", "Event");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("MatchLinksHistory", "Event");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MatchParticipant", b =>
                {
                    b.Property<Guid>("MatchID")
                        .HasColumnType("uniqueidentifier");

                    b.Property<long>("XboxUserID")
                        .HasColumnType("bigint");

                    b.Property<float>("Accuracy")
                        .HasColumnType("real");

                    b.Property<int>("Assists")
                        .HasColumnType("int");

                    b.Property<TimeSpan>("AverageLife")
                        .HasColumnType("time");

                    b.Property<int>("Betrayals")
                        .HasColumnType("int");

                    b.Property<int>("CalloutAssists")
                        .HasColumnType("int");

                    b.Property<int>("DamageDealt")
                        .HasColumnType("int");

                    b.Property<int>("DamageTaken")
                        .HasColumnType("int");

                    b.Property<int>("Deaths")
                        .HasColumnType("int");

                    b.Property<float>("Kda")
                        .HasColumnType("real");

                    b.Property<int>("Kills")
                        .HasColumnType("int");

                    b.Property<int>("MaxKillingSpree")
                        .HasColumnType("int");

                    b.Property<int>("MeleeKills")
                        .HasColumnType("int");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<int>("PowerWeaponKills")
                        .HasColumnType("int");

                    b.Property<int>("ShotsFired")
                        .HasColumnType("int");

                    b.Property<int>("ShotsHit")
                        .HasColumnType("int");

                    b.Property<int>("Suicides")
                        .HasColumnType("int");

                    b.Property<int>("TeamID")
                        .HasColumnType("int");

                    b.HasKey("MatchID", "XboxUserID");

                    b.HasIndex("XboxUserID");

                    b.ToTable("MatchParticipants", "Infinite");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("MatchParticipantsHistory", "Infinite");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Medal", b =>
                {
                    b.Property<long>("MedalID")
                        .HasColumnType("bigint");

                    b.Property<string>("Description")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("MedalDifficultyID")
                        .HasColumnType("int");

                    b.Property<string>("MedalName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("MedalTypeID")
                        .HasColumnType("int");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<int>("PersonalScore")
                        .HasColumnType("int");

                    b.Property<int>("SortingWeight")
                        .HasColumnType("int");

                    b.Property<int>("SpriteIndex")
                        .HasColumnType("int");

                    b.HasKey("MedalID");

                    b.HasIndex("MedalDifficultyID");

                    b.HasIndex("MedalTypeID");

                    b.ToTable("Medals", "Infinite");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("MedalsHistory", "Infinite");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MedalDifficulty", b =>
                {
                    b.Property<int>("MedalDifficultyID")
                        .HasColumnType("int");

                    b.Property<string>("MedalDifficultyName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.HasKey("MedalDifficultyID");

                    b.ToTable("MedalDifficulties", "Infinite");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("MedalDifficultiesHistory", "Infinite");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MedalEarned", b =>
                {
                    b.Property<long>("MedalID")
                        .HasColumnType("bigint");

                    b.Property<Guid>("MatchID")
                        .HasColumnType("uniqueidentifier");

                    b.Property<long>("XboxUserID")
                        .HasColumnType("bigint");

                    b.Property<int>("Count")
                        .HasColumnType("int");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<int>("TotalPersonalScoreAwarded")
                        .HasColumnType("int");

                    b.HasKey("MedalID", "MatchID", "XboxUserID");

                    b.HasIndex("MatchID", "XboxUserID");

                    b.ToTable("MedalEarned", "Infinite");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("MedalEarnedHistory", "Infinite");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MedalType", b =>
                {
                    b.Property<int>("MedalTypeID")
                        .HasColumnType("int");

                    b.Property<string>("MedalTypeName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.HasKey("MedalTypeID");

                    b.ToTable("MedalTypes", "Infinite");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("MedalTypesHistory", "Infinite");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Password", b =>
                {
                    b.Property<int>("PersonID")
                        .HasColumnType("int");

                    b.Property<string>("Hash")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<string>("Salt")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("PersonID");

                    b.ToTable("Passwords", "Account");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("PasswordsHistory", "Account");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Person", b =>
                {
                    b.Property<int>("PersonID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("PersonID"));

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<long>("XboxUserID")
                        .HasColumnType("bigint");

                    b.HasKey("PersonID");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.HasIndex("XboxUserID")
                        .IsUnique();

                    b.ToTable("Persons", "Event");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("PersonsHistory", "Event");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Season", b =>
                {
                    b.Property<int>("SeasonID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("SeasonID"));

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<string>("SeasonName")
                        .IsRequired()
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)");

                    b.HasKey("SeasonID");

                    b.HasIndex("SeasonName")
                        .IsUnique();

                    b.ToTable("Seasons", "Event");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("SeasonsHistory", "Event");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.SeasonMatch", b =>
                {
                    b.Property<int>("SeasonMatchID")
                        .HasColumnType("int");

                    b.Property<int>("AwayTeamID")
                        .HasColumnType("int");

                    b.Property<int>("HomeTeamID")
                        .HasColumnType("int");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<DateTime>("ScheduledTime")
                        .HasColumnType("datetime2");

                    b.Property<int>("SeasonID")
                        .HasColumnType("int");

                    b.HasKey("SeasonMatchID");

                    b.HasIndex("AwayTeamID");

                    b.HasIndex("HomeTeamID");

                    b.ToTable("SeasonMatches", "Event");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("SeasonMatchesHistory", "Event");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Team", b =>
                {
                    b.Property<int>("TeamID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("TeamID"));

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<int>("SeasonID")
                        .HasColumnType("int");

                    b.Property<string>("TeamName")
                        .HasMaxLength(30)
                        .HasColumnType("nvarchar(30)");

                    b.HasKey("TeamID");

                    b.HasIndex("SeasonID");

                    b.HasIndex("TeamName", "SeasonID")
                        .IsUnique()
                        .HasFilter("[TeamName] IS NOT NULL");

                    b.ToTable("Teams", "Event");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("TeamsHistory", "Event");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.TeamPlayer", b =>
                {
                    b.Property<int>("TeamPlayerID")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("TeamPlayerID"));

                    b.Property<bool>("IsCaptain")
                        .HasColumnType("bit");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.Property<int>("PlayerID")
                        .HasColumnType("int");

                    b.Property<int>("TeamID")
                        .HasColumnType("int");

                    b.HasKey("TeamPlayerID");

                    b.HasIndex("PlayerID");

                    b.HasIndex("TeamID");

                    b.ToTable("TeamPlayers", "Event");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("TeamPlayersHistory", "Event");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.XboxUser", b =>
                {
                    b.Property<long>("XboxUserID")
                        .HasColumnType("bigint");

                    b.Property<string>("Gamertag")
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("PeriodEnd")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodEnd");

                    b.Property<DateTime>("PeriodStart")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasColumnType("datetime2")
                        .HasColumnName("PeriodStart");

                    b.HasKey("XboxUserID");

                    b.ToTable("XboxUsers", "Xbox");

                    b.ToTable(tb => tb.IsTemporal(ttb =>
                            {
                                ttb.UseHistoryTable("XboxUsersHistory", "Xbox");
                                ttb
                                    .HasPeriodStart("PeriodStart")
                                    .HasColumnName("PeriodStart");
                                ttb
                                    .HasPeriodEnd("PeriodEnd")
                                    .HasColumnName("PeriodEnd");
                            }));
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MatchLink", b =>
                {
                    b.HasOne("GrifballWebApp.Database.Models.Match", "Match")
                        .WithOne("MatchLink")
                        .HasForeignKey("GrifballWebApp.Database.Models.MatchLink", "MatchID");

                    b.HasOne("GrifballWebApp.Database.Models.SeasonMatch", "SeasonMatch")
                        .WithOne("MatchLink")
                        .HasForeignKey("GrifballWebApp.Database.Models.MatchLink", "SeasonMatchID");

                    b.Navigation("Match");

                    b.Navigation("SeasonMatch");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MatchParticipant", b =>
                {
                    b.HasOne("GrifballWebApp.Database.Models.Match", "Match")
                        .WithMany("MatchParticipants")
                        .HasForeignKey("MatchID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("GrifballWebApp.Database.Models.XboxUser", "XboxUser")
                        .WithMany("MatchParticipants")
                        .HasForeignKey("XboxUserID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Match");

                    b.Navigation("XboxUser");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Medal", b =>
                {
                    b.HasOne("GrifballWebApp.Database.Models.MedalDifficulty", "MedalDifficulty")
                        .WithMany("Medals")
                        .HasForeignKey("MedalDifficultyID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("GrifballWebApp.Database.Models.MedalType", "MedalType")
                        .WithMany("Medals")
                        .HasForeignKey("MedalTypeID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("MedalDifficulty");

                    b.Navigation("MedalType");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MedalEarned", b =>
                {
                    b.HasOne("GrifballWebApp.Database.Models.Medal", "Medal")
                        .WithMany("MedalEarned")
                        .HasForeignKey("MedalID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("GrifballWebApp.Database.Models.MatchParticipant", "MatchParticipant")
                        .WithMany("MedalEarned")
                        .HasForeignKey("MatchID", "XboxUserID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("MatchParticipant");

                    b.Navigation("Medal");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Password", b =>
                {
                    b.HasOne("GrifballWebApp.Database.Models.Person", "Person")
                        .WithOne("Password")
                        .HasForeignKey("GrifballWebApp.Database.Models.Password", "PersonID");

                    b.Navigation("Person");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Person", b =>
                {
                    b.HasOne("GrifballWebApp.Database.Models.XboxUser", "XboxUser")
                        .WithOne("Person")
                        .HasForeignKey("GrifballWebApp.Database.Models.Person", "XboxUserID");

                    b.Navigation("XboxUser");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.SeasonMatch", b =>
                {
                    b.HasOne("GrifballWebApp.Database.Models.Team", "AwayTeam")
                        .WithMany("AwayMatches")
                        .HasForeignKey("AwayTeamID")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("GrifballWebApp.Database.Models.Team", "HomeTeam")
                        .WithMany("HomeMatches")
                        .HasForeignKey("HomeTeamID")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.HasOne("GrifballWebApp.Database.Models.Season", "Season")
                        .WithMany("SeasonMatches")
                        .HasForeignKey("SeasonMatchID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("AwayTeam");

                    b.Navigation("HomeTeam");

                    b.Navigation("Season");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Team", b =>
                {
                    b.HasOne("GrifballWebApp.Database.Models.Season", "Season")
                        .WithMany("Teams")
                        .HasForeignKey("SeasonID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Season");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.TeamPlayer", b =>
                {
                    b.HasOne("GrifballWebApp.Database.Models.Person", "Person")
                        .WithMany("TeamPlayers")
                        .HasForeignKey("PlayerID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("GrifballWebApp.Database.Models.Team", "Team")
                        .WithMany("TeamPlayers")
                        .HasForeignKey("TeamID")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Person");

                    b.Navigation("Team");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Match", b =>
                {
                    b.Navigation("MatchLink");

                    b.Navigation("MatchParticipants");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MatchParticipant", b =>
                {
                    b.Navigation("MedalEarned");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Medal", b =>
                {
                    b.Navigation("MedalEarned");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MedalDifficulty", b =>
                {
                    b.Navigation("Medals");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.MedalType", b =>
                {
                    b.Navigation("Medals");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Person", b =>
                {
                    b.Navigation("Password");

                    b.Navigation("TeamPlayers");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Season", b =>
                {
                    b.Navigation("SeasonMatches");

                    b.Navigation("Teams");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.SeasonMatch", b =>
                {
                    b.Navigation("MatchLink");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.Team", b =>
                {
                    b.Navigation("AwayMatches");

                    b.Navigation("HomeMatches");

                    b.Navigation("TeamPlayers");
                });

            modelBuilder.Entity("GrifballWebApp.Database.Models.XboxUser", b =>
                {
                    b.Navigation("MatchParticipants");

                    b.Navigation("Person");
                });
#pragma warning restore 612, 618
        }
    }
}
