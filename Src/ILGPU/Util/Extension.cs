// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Extension.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.Util
{
    /// <summary>
    /// An abstract runtime-object extension instance.
    /// </summary>
    public abstract class Extension : DisposeBase { }

    /// <summary>
    /// An abstract runtime-object extension instance.
    /// </summary>
    public abstract class CachedExtension : Extension, ICache
    {
        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        public virtual void ClearCache(ClearCacheMode mode) { }
    }

    /// <summary>
    /// An abstract extension object.
    /// </summary>
    /// <typeparam name="TExtension">The underlying extension type.</typeparam>
    public interface IExtensionObject<TExtension>
        where TExtension : Extension
    {
        /// <summary>
        /// Registers a new backend extensions.
        /// </summary>
        /// <typeparam name="T">The extension type.</typeparam>
        /// <param name="extension">The extension instance to register.</param>
        void RegisterExtension<T>(T extension) where T : TExtension;

        /// <summary>
        /// Retrieves a backend extension of the given type.
        /// </summary>
        /// <typeparam name="T">The extension type.</typeparam>
        /// <returns>The extension instance.</returns>
        T GetExtension<T>() where T : TExtension;

        /// <summary>
        /// Tries to retrieve a backend extension of the given type.
        /// </summary>
        /// <typeparam name="T">The extension type.</typeparam>
        /// <param name="extension">The extension instance.</param>
        /// <returns>True, if the extension could be retrieved.</returns>
        bool TryGetExtension<T>([NotNullWhen(true)] out T? extension)
            where T : TExtension;

        /// <summary>
        /// Executes the given action for each registered extension.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        void ForEachExtension(Action<TExtension> action);
    }

    /// <summary>
    /// An extension base object that provides a default implementation of an
    /// <see cref="IExtensionObject{TExtension}"/>.
    /// </summary>
    /// <typeparam name="TExtension">The underlying extension type.</typeparam>
    public abstract class ExtensionBase<TExtension> :
        DisposeBase,
        IExtensionObject<TExtension>
        where TExtension : Extension
    {
        #region Instance

        /// <summary>
        /// The associated backend extensions.
        /// </summary>
        private readonly Dictionary<Type, TExtension> extensions =
            new Dictionary<Type, TExtension>();

        #endregion

        #region Methods

        /// <summary>
        /// Registers a new extensions.
        /// </summary>
        /// <typeparam name="T">The extension type.</typeparam>
        /// <param name="extension">The extension instance to register.</param>
        /// <remarks>This method is not thread-safe.</remarks>
        public void RegisterExtension<T>(T extension) where T : TExtension
        {
            Debug.Assert(extension != null, "Invalid extension");
            extensions[typeof(T)] = extension;
        }

        /// <summary>
        /// Retrieves a extension of the given type.
        /// </summary>
        /// <typeparam name="T">The extension type name.</typeparam>
        /// <returns>The extension instance.</returns>
        public T GetExtension<T>() where T : TExtension
        {
            var extension = extensions[typeof(T)] as T;
            Debug.Assert(extension != null, "Invalid backend extension");
            return extension;
        }

        /// <summary>
        /// Tries to retrieve a backend extension of the given type.
        /// </summary>
        /// <typeparam name="T">The extension type.</typeparam>
        /// <param name="extension">The extension instance.</param>
        /// <returns>True, if the extension could be retrieved.</returns>
        public bool TryGetExtension<T>([NotNullWhen(true)] out T? extension)
            where T : TExtension
        {
            extension = null;
            if (!extensions.TryGetValue(typeof(T), out TExtension? ext))
                return false;
            extension = ext as T;
            Debug.Assert(extension != null, "Invalid backend extension");
            return true;
        }

        /// <summary>
        /// Executes the given action for each registered extension.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        public void ForEachExtension(Action<TExtension> action)
        {
            foreach (var extension in extensions.Values)
                action(extension);
        }

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            foreach (var extension in extensions.Values)
                extension.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }

    /// <summary>
    /// An extension base object that provides a default implementation of an
    /// <see cref="IExtensionObject{TExtension}"/> including caches.
    /// </summary>
    /// <typeparam name="TExtension">The underlying extension type.</typeparam>
    public abstract class CachedExtensionBase<TExtension> :
        ExtensionBase<TExtension>,
        ICache
        where TExtension : CachedExtension
    {
        #region Methods

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        /// <remarks>This method is not thread-safe.</remarks>
        public virtual void ClearCache(ClearCacheMode mode) =>
            ForEachExtension(extension => extension.ClearCache(mode));

        #endregion
    }
}
