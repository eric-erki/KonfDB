﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace KonfDB.Engine.Database.EntityFramework
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class KonfDBEntities : DbContext
    {
        public KonfDBEntities()
            : base("name=KonfDBEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<Membership> Memberships { get; set; }
        public virtual DbSet<Role> Roles { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Application> Applications { get; set; }
        public virtual DbSet<Audit> Audits { get; set; }
        public virtual DbSet<AuditArea> AuditAreas { get; set; }
        public virtual DbSet<Category> Categories { get; set; }
        public virtual DbSet<Environment> Environments { get; set; }
        public virtual DbSet<EnvironmentType> EnvironmentTypes { get; set; }
        public virtual DbSet<Mapping> Mappings { get; set; }
        public virtual DbSet<Parameter> Parameters { get; set; }
        public virtual DbSet<Region> Regions { get; set; }
        public virtual DbSet<Server> Servers { get; set; }
        public virtual DbSet<Suite> Suites { get; set; }
        public virtual DbSet<SuiteUser> SuiteUsers { get; set; }
        public virtual DbSet<City> Cities { get; set; }
        public virtual DbSet<Country> Countries { get; set; }
        public virtual DbSet<Option> Options { get; set; }
    }
}
