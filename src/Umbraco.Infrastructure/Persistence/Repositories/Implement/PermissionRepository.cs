using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Extensions.Logging;
using NPoco;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Infrastructure.Persistence.Dtos;
using Umbraco.Extensions;

namespace Umbraco.Cms.Infrastructure.Persistence.Repositories.Implement
{
    /// <summary>
    ///     A (sub) repository that exposes functionality to modify assigned permissions to a node
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <remarks>
    ///     This repo implements the base <see cref="NPocoRepositoryBase{TId,TEntity}" /> class so that permissions can be
    ///     queued to be persisted
    ///     like the normal repository pattern but the standard repository Get commands don't apply and will throw
    ///     <see cref="NotImplementedException" />
    /// </remarks>
    internal class PermissionRepository<TEntity> : EntityRepositoryBase<int, ContentPermissionSet>
        where TEntity : class, IEntity
    {
        public PermissionRepository(IScopeAccessor scopeAccessor, AppCaches cache,
            ILogger<PermissionRepository<TEntity>> logger)
            : base(scopeAccessor, cache, logger)
        {
        }

        /// <summary>
        ///     Returns explicitly defined permissions for a user group for any number of nodes
        /// </summary>
        /// <param name="groupIds">
        ///     The group ids to lookup permissions for
        /// </param>
        /// <param name="entityIds"></param>
        /// <returns></returns>
        /// <remarks>
        ///     This method will not support passing in more than 2000 group IDs when also passing in entity IDs.
        /// </remarks>
        public EntityPermissionCollection GetPermissionsForEntities(int[] groupIds, params int[] entityIds)
        {
            var result = new EntityPermissionCollection();

            if (entityIds.Length == 0)
            {
                foreach (IEnumerable<int> group in groupIds.InGroupsOf(Constants.Sql.MaxParameterCount))
                {
                    Sql<ISqlContext> sql = Sql()
                        .SelectAll()
                        .From<UserGroup2NodePermissionDto>()
                        .Where<UserGroup2NodePermissionDto>(dto => group.Contains(dto.UserGroupId));

                    List<UserGroup2NodePermissionDto> permissions =
                        AmbientScope.Database.Fetch<UserGroup2NodePermissionDto>(sql);
                    foreach (EntityPermission permission in ConvertToPermissionList(permissions))
                    {
                        result.Add(permission);
                    }
                }
            }
            else
            {
                foreach (IEnumerable<int> group in entityIds.InGroupsOf(Constants.Sql.MaxParameterCount -
                                                                        groupIds.Length))
                {
                    Sql<ISqlContext> sql = Sql()
                        .SelectAll()
                        .From<UserGroup2NodePermissionDto>()
                        .Where<UserGroup2NodePermissionDto>(dto =>
                            groupIds.Contains(dto.UserGroupId) && group.Contains(dto.NodeId));

                    List<UserGroup2NodePermissionDto> permissions =
                        AmbientScope.Database.Fetch<UserGroup2NodePermissionDto>(sql);
                    foreach (EntityPermission permission in ConvertToPermissionList(permissions))
                    {
                        result.Add(permission);
                    }
                }
            }

            return result;
        }

        /// <summary>
        ///     Returns permissions directly assigned to the content items for all user groups
        /// </summary>
        /// <param name="entityIds"></param>
        /// <returns></returns>
        public IEnumerable<EntityPermission> GetPermissionsForEntities(int[] entityIds)
        {
            Sql<ISqlContext> sql = Sql()
                .SelectAll()
                .From<UserGroup2NodePermissionDto>()
                .Where<UserGroup2NodePermissionDto>(dto => entityIds.Contains(dto.NodeId))
                .OrderBy<UserGroup2NodePermissionDto>(dto => dto.NodeId);

            List<UserGroup2NodePermissionDto> result = AmbientScope.Database.Fetch<UserGroup2NodePermissionDto>(sql);
            return ConvertToPermissionList(result);
        }

        /// <summary>
        ///     Returns permissions directly assigned to the content item for all user groups
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public EntityPermissionCollection GetPermissionsForEntity(int entityId)
        {
            Sql<ISqlContext> sql = Sql()
                .SelectAll()
                .From<UserGroup2NodePermissionDto>()
                .Where<UserGroup2NodePermissionDto>(dto => dto.NodeId == entityId)
                .OrderBy<UserGroup2NodePermissionDto>(dto => dto.NodeId);

            List<UserGroup2NodePermissionDto> result = AmbientScope.Database.Fetch<UserGroup2NodePermissionDto>(sql);
            return ConvertToPermissionList(result);
        }

        /// <summary>
        ///     Assigns the same permission set for a single group to any number of entities
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="permissions"></param>
        /// <param name="entityIds"></param>
        /// <remarks>
        ///     This will first clear the permissions for this user and entities and recreate them
        /// </remarks>
        public void ReplacePermissions(int groupId, IEnumerable<char> permissions, params int[] entityIds)
        {
            if (entityIds.Length == 0)
            {
                return;
            }

            IUmbracoDatabase db = AmbientScope.Database;

            var sql =
                "DELETE FROM umbracoUserGroup2NodePermission WHERE userGroupId = @groupId AND nodeId in (@nodeIds)";
            foreach (IEnumerable<int> group in entityIds.InGroupsOf(Constants.Sql.MaxParameterCount))
            {
                db.Execute(sql, new { groupId, nodeIds = group });
            }

            var toInsert = new List<UserGroup2NodePermissionDto>();
            foreach (var p in permissions)
            {
                foreach (var e in entityIds)
                {
                    toInsert.Add(new UserGroup2NodePermissionDto
                    {
                        NodeId = e, Permission = p.ToString(CultureInfo.InvariantCulture), UserGroupId = groupId
                    });
                }
            }

            db.BulkInsertRecords(toInsert);
        }

        /// <summary>
        ///     Assigns one permission for a user to many entities
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="permission"></param>
        /// <param name="entityIds"></param>
        public void AssignPermission(int groupId, char permission, params int[] entityIds)
        {
            IUmbracoDatabase db = AmbientScope.Database;

            var sql =
                "DELETE FROM umbracoUserGroup2NodePermission WHERE userGroupId = @groupId AND permission=@permission AND nodeId in (@entityIds)";
            db.Execute(sql,
                new { groupId, permission = permission.ToString(CultureInfo.InvariantCulture), entityIds });

            UserGroup2NodePermissionDto[] actions = entityIds.Select(id => new UserGroup2NodePermissionDto
            {
                NodeId = id, Permission = permission.ToString(CultureInfo.InvariantCulture), UserGroupId = groupId
            }).ToArray();

            db.BulkInsertRecords(actions);
        }

        /// <summary>
        ///     Assigns one permission to an entity for multiple groups
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="permission"></param>
        /// <param name="groupIds"></param>
        public void AssignEntityPermission(TEntity entity, char permission, IEnumerable<int> groupIds)
        {
            IUmbracoDatabase db = AmbientScope.Database;
            var groupIdsA = groupIds.ToArray();

            const string sql =
                "DELETE FROM umbracoUserGroup2NodePermission WHERE nodeId = @nodeId AND permission = @permission AND userGroupId in (@groupIds)";
            db.Execute(sql,
                new
                {
                    nodeId = entity.Id,
                    permission = permission.ToString(CultureInfo.InvariantCulture),
                    groupIds = groupIdsA
                });

            UserGroup2NodePermissionDto[] actions = groupIdsA.Select(id => new UserGroup2NodePermissionDto
            {
                NodeId = entity.Id, Permission = permission.ToString(CultureInfo.InvariantCulture), UserGroupId = id
            }).ToArray();

            db.BulkInsertRecords(actions);
        }

        /// <summary>
        ///     Assigns permissions to an entity for multiple group/permission entries
        /// </summary>
        /// <param name="permissionSet">
        /// </param>
        /// <remarks>
        ///     This will first clear the permissions for this entity then re-create them
        /// </remarks>
        public void ReplaceEntityPermissions(EntityPermissionSet permissionSet)
        {
            IUmbracoDatabase db = AmbientScope.Database;

            const string sql = "DELETE FROM umbracoUserGroup2NodePermission WHERE nodeId = @nodeId";
            db.Execute(sql, new { nodeId = permissionSet.EntityId });

            var toInsert = new List<UserGroup2NodePermissionDto>();
            foreach (EntityPermission entityPermission in permissionSet.PermissionsSet)
            {
                foreach (var permission in entityPermission.AssignedPermissions)
                {
                    toInsert.Add(new UserGroup2NodePermissionDto
                    {
                        NodeId = permissionSet.EntityId,
                        Permission = permission,
                        UserGroupId = entityPermission.UserGroupId
                    });
                }
            }

            db.BulkInsertRecords(toInsert);
        }

        /// <summary>
        ///     Used to add or update entity permissions during a content item being updated
        /// </summary>
        /// <param name="entity"></param>
        protected override void PersistNewItem(ContentPermissionSet entity) =>
            //does the same thing as update
            PersistUpdatedItem(entity);

        /// <summary>
        ///     Used to add or update entity permissions during a content item being updated
        /// </summary>
        /// <param name="entity"></param>
        protected override void PersistUpdatedItem(ContentPermissionSet entity)
        {
            var asIEntity = (IEntity)entity;
            if (asIEntity.HasIdentity == false)
            {
                throw new InvalidOperationException("Cannot create permissions for an entity without an Id");
            }

            ReplaceEntityPermissions(entity);
        }

        private static EntityPermissionCollection ConvertToPermissionList(
            IEnumerable<UserGroup2NodePermissionDto> result)
        {
            var permissions = new EntityPermissionCollection();
            IEnumerable<IGrouping<int, UserGroup2NodePermissionDto>> nodePermissions = result.GroupBy(x => x.NodeId);
            foreach (IGrouping<int, UserGroup2NodePermissionDto> np in nodePermissions)
            {
                IEnumerable<IGrouping<int, UserGroup2NodePermissionDto>> userGroupPermissions =
                    np.GroupBy(x => x.UserGroupId);
                foreach (IGrouping<int, UserGroup2NodePermissionDto> permission in userGroupPermissions)
                {
                    var perms = permission.Select(x => x.Permission).Distinct().ToArray();
                    permissions.Add(new EntityPermission(permission.Key, np.Key, perms));
                }
            }

            return permissions;
        }

        #region Not implemented (don't need to for the purposes of this repo)

        protected override ContentPermissionSet PerformGet(int id) =>
            throw new InvalidOperationException("This method won't be implemented.");

        protected override IEnumerable<ContentPermissionSet> PerformGetAll(params int[] ids) =>
            throw new InvalidOperationException("This method won't be implemented.");

        protected override IEnumerable<ContentPermissionSet> PerformGetByQuery(IQuery<ContentPermissionSet> query) =>
            throw new InvalidOperationException("This method won't be implemented.");

        protected override Sql<ISqlContext> GetBaseQuery(bool isCount) =>
            throw new InvalidOperationException("This method won't be implemented.");

        protected override string GetBaseWhereClause() =>
            throw new InvalidOperationException("This method won't be implemented.");

        protected override IEnumerable<string> GetDeleteClauses() => new List<string>();

        protected override void PersistDeletedItem(ContentPermissionSet entity) =>
            throw new InvalidOperationException("This method won't be implemented.");

        #endregion
    }
}
