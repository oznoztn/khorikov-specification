﻿using System;
using System.Linq;
using System.Linq.Expressions;

namespace Logic.Movies
{
    // Bu specification aslında bir şey yapmıyor.
    // Ne olursa olsun true dönderen bir predicate'in anlamı bütün nesneleri döndür demekle aynıdır, herhangi bir filtreleme yok.
    // Peki bunu neden oluşturduk o halde?
    // Bunun için MovieListViewModel sınıfı içerisindeki Search metodunu incele!

    // Bunu Null Object Pattern'inin uygulanmasına örnek gösterebiliriz.
    internal sealed class IdentitySpecification<T> : Specification<T>
    {
        public override Expression<Func<T, bool>> ToExpression()
        {
            return x => true;
        }
    }


    public abstract class Specification<T>
    {
        public static readonly Specification<T> All = new IdentitySpecification<T>();

        public bool IsSatisfiedBy(T entity)
        {
            Func<T, bool> predicate = ToExpression().Compile();
            return predicate(entity);
        }

        public abstract Expression<Func<T, bool>> ToExpression();

        // DİKKAT ET: Specification<T> döndürüyor, And<T> değil!
        public Specification<T> And(Specification<T> specification)
        {
            // Burada küçük bir optimizasyon yapıyoruz.
            // Bunun nedeni şu:
            // Bütün nesneleri getirmek için IdentitySpecification kullandığımızda
            // ORM, SQL cümlesinde fazladan bir tane Where ifadesi oluşturacaktır.
            // Biz bu anlamsız ifadeyi neden sorgumuzda barındıralım ki?
            // Yararı yok, belki minicik de olsa, zararı var.

            // Eğer LEFT OPERAND, All spesifikasyonuya sadece RIGHT OPERAND'ı dönder. Left operand bir şey ifade etmiyor.
            if (this == All)
                return specification;
            // Tam tersi:
            if (specification == All)
                return this;

            return new AndSpecification<T>(this, specification);
        }

        // DİKKAT ET: Specification<T> döndürüyor, OrSpecification<T> değil!
        public Specification<T> Or(Specification<T> specification)
        {
            // Optimizasyon
            if (this == All || specification == All)
                return All;

            return new OrSpecification<T>(this, specification);
        }

        // DİKKAT ET: Specification<T> döndürüyor, NotSpecification<T> değil!
        public Specification<T> Not()
        {
            return new NotSpecification<T>(this);
        }
    }

    // AND, OR, NOT ve IdentitySpecification spek. sınıflarının hepsinin INTERNAL olmasına dikkat et.
    // Burada sağladığımız şey client kodunun bu objeleri asla direk olarak örnekleyememesini sağlamak
    // Sadece Specification<T> sınıfında tanımladığımız metotlar aracılığıyla
    //      bu objelerin sağladığı fonksiyonelliklere erişebiliyorlar.
    // 
    // Dikkat et Spec<T> sınıfındaki söz konusu metotlar Specification<T> tipini dönderiyor!
    // Client AND, OR, NOTSpecification sınıflarında habersiz.

    // INTERNAL olmasına dikkat.
    internal sealed class AndSpecification<T> : Specification<T>
    {
        private readonly Specification<T> _left;
        private readonly Specification<T> _right;

        public AndSpecification(Specification<T> left, Specification<T> right)
        {
            _right = right;
            _left = left;
        }

        public override Expression<Func<T, bool>> ToExpression()
        {
            Expression<Func<T, bool>> leftExpression = _left.ToExpression();
            Expression<Func<T, bool>> rightExpression = _right.ToExpression();

            BinaryExpression andExpression = Expression.AndAlso(leftExpression.Body, rightExpression.Body);

            return Expression.Lambda<Func<T, bool>>(andExpression, leftExpression.Parameters.Single());
        }
    }

    // INTERNAL olmasına dikkat.
    internal sealed class OrSpecification<T> : Specification<T>
    {
        private readonly Specification<T> _left;
        private readonly Specification<T> _right;

        public OrSpecification(Specification<T> left, Specification<T> right)
        {
            _right = right;
            _left = left;
        }

        public override Expression<Func<T, bool>> ToExpression()
        {
            Expression<Func<T, bool>> leftExpression = _left.ToExpression();
            Expression<Func<T, bool>> rightExpression = _right.ToExpression();

            BinaryExpression orExpression = Expression.OrElse(leftExpression.Body, rightExpression.Body);

            return Expression.Lambda<Func<T, bool>>(orExpression, leftExpression.Parameters.Single());
        }
    }


    internal sealed class NotSpecification<T> : Specification<T>
    {
        private readonly Specification<T> _specification;

        public NotSpecification(Specification<T> specification)
        {
            _specification = specification;
        }

        public override Expression<Func<T, bool>> ToExpression()
        {
            Expression<Func<T, bool>> expression = _specification.ToExpression();
            UnaryExpression notExpression = Expression.Not(expression.Body);

            return Expression.Lambda<Func<T, bool>>(notExpression, expression.Parameters.Single());
        }
    }


    public sealed class MovieForKidsSpecification : Specification<Movie>
    {
        public override Expression<Func<Movie, bool>> ToExpression()
        {
            return movie => movie.MpaaRating <= MpaaRating.PG;
        }
    }


    public sealed class AvailableOnCDSpecification : Specification<Movie>
    {
        private const int MonthsBeforeDVDIsOut = 6;

        public override Expression<Func<Movie, bool>> ToExpression()
        {
            return movie => movie.ReleaseDate <= DateTime.Now.AddMonths(-MonthsBeforeDVDIsOut);
        }
    }


    public sealed class MovieDirectedBySpecification : Specification<Movie>
    {
        private readonly string _director;

        public MovieDirectedBySpecification(string director)
        {
            _director = director;
        }

        public override Expression<Func<Movie, bool>> ToExpression()
        {
            return movie => movie.Director.Name == _director;
        }
    }
}
