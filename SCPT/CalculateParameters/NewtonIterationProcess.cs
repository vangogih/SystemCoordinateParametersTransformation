﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Extreme.Mathematics;
using Extreme.Mathematics.LinearAlgebra;

namespace CalculateParameters
{
    public class NewtonIterationProcess
    {
        private const int MinListCount = 3;

        public List<Coordinates> SourceSystemCoordinates { get; }
        public List<Coordinates> DestinationSystemCoordinates { get; }

        public Matrix<double> TransformParametersMatrix { get; private set; }
        public Matrix<double> MeanSquareErrorsMatrix { get; private set; }

        public NewtonIterationProcess(List<Coordinates> source, List<Coordinates> destination)
        {
            if (source == null)
                throw new NullReferenceException("source list cannot be null");
            if (destination == null)
                throw new NullReferenceException("destination list cannot be null");
            if (source.Count <= MinListCount)
                throw new ArgumentException("source list count cannot be less when 4");
            if (destination.Count <= MinListCount)
                throw new ArgumentException("source list count cannot be less when 4");
            if (source.Count != destination.Count)
                throw new ArgumentException("source list and destination list must be of the same length");

            SourceSystemCoordinates = source;
            DestinationSystemCoordinates = destination;
        }

        public void CalculateTransformationParameters()
        {
            var aMatrix = FormingAMatrix();
            var yMatrix = FormingYMatrix();
            var paramsTransformMatrix = GetMatrixWithTransformParameters(aMatrix, yMatrix);
            var vMatrix = GetVMatrix(aMatrix, yMatrix, paramsTransformMatrix);
            var fCoefficient = CalculateFCoefficient(vMatrix);
            var mCoefficient = CalculateMCoefficient(fCoefficient);
            var qMatrix = GetQMatrix(aMatrix);
            var mceMatrix = GetMeanSquareErrorsMatrix(qMatrix, mCoefficient);

            TransformParametersMatrix = paramsTransformMatrix;
            MeanSquareErrorsMatrix = mceMatrix;
        }

        private Matrix<double> FormingCoordinateMatrix(List<Coordinates> list)
        {
            var vectorMatrix = Matrix.Create<double>(list.Count, 3); // matrix [nx3]
            for (int i = 0; i < list.Count; i++)
            {
                vectorMatrix[i, 0] = list[i].X;
                vectorMatrix[i, 1] = list[i].Y;
                vectorMatrix[i, 2] = list[i].Z;
            }

            return vectorMatrix;
        }

        private Matrix<double> FormingAMatrix()
        {
            var aMatrix = Matrix.Create<double>(SourceSystemCoordinates.Count * 3, 7);

            for (int matrixIndex = 0, listIndex = 0;
                matrixIndex < SourceSystemCoordinates.Count * 3 || listIndex < SourceSystemCoordinates.Count;
                matrixIndex += 3, listIndex++)
            {
                aMatrix[matrixIndex, 0] = 1;
                aMatrix[matrixIndex, 1] = 0;
                aMatrix[matrixIndex, 2] = 0;
                aMatrix[matrixIndex, 3] = 0;
                aMatrix[matrixIndex, 4] = -SourceSystemCoordinates[listIndex].Z;
                aMatrix[matrixIndex, 5] = SourceSystemCoordinates[listIndex].Y;
                aMatrix[matrixIndex, 6] = SourceSystemCoordinates[listIndex].X;

                aMatrix[matrixIndex + 1, 0] = 0;
                aMatrix[matrixIndex + 1, 1] = 1;
                aMatrix[matrixIndex + 1, 2] = 0;
                aMatrix[matrixIndex + 1, 3] = SourceSystemCoordinates[listIndex].Z;
                aMatrix[matrixIndex + 1, 4] = 0;
                aMatrix[matrixIndex + 1, 5] = -SourceSystemCoordinates[listIndex].X;
                aMatrix[matrixIndex + 1, 6] = SourceSystemCoordinates[listIndex].Y;

                aMatrix[matrixIndex + 2, 0] = 0;
                aMatrix[matrixIndex + 2, 1] = 0;
                aMatrix[matrixIndex + 2, 2] = 1;
                aMatrix[matrixIndex + 2, 3] = -SourceSystemCoordinates[listIndex].Y;
                aMatrix[matrixIndex + 2, 4] = SourceSystemCoordinates[listIndex].X;
                aMatrix[matrixIndex + 2, 5] = 0;
                aMatrix[matrixIndex + 2, 6] = SourceSystemCoordinates[listIndex].Z;
            }

            return aMatrix;
        }

        private Matrix<double> FormingYMatrix()
        {
            var yMatrix = Matrix.Create<double>(SourceSystemCoordinates.Count * 3, 1);

            for (int matrixIndex = 0, listIndex = 0;
                matrixIndex < SourceSystemCoordinates.Count * 3 && listIndex < SourceSystemCoordinates.Count;
                matrixIndex += 3, listIndex++)
            {
                yMatrix[matrixIndex, 0] =
                    DestinationSystemCoordinates[listIndex].X - SourceSystemCoordinates[listIndex].X;
                yMatrix[matrixIndex + 1, 0] =
                    DestinationSystemCoordinates[listIndex].Y - SourceSystemCoordinates[listIndex].Y;
                yMatrix[matrixIndex + 2, 0] =
                    DestinationSystemCoordinates[listIndex].Z - SourceSystemCoordinates[listIndex].Z;
            }

            return yMatrix;
        }

        private Matrix<double> GetMatrixWithTransformParameters(Matrix<double> aMatrix, Matrix<double> yMatrix)
        {
            var currPMatrix = Matrix.Create<double>(7, 1);
            var prevPMatrix = Matrix.Create<double>(7, 1);
            for (int i = 0; i < prevPMatrix.RowCount; i++)
                prevPMatrix[i, 0] = double.MaxValue;

            while (IsSubtractMatrixValuesLessWhenDelta(prevPMatrix, currPMatrix, 0))
            {
                prevPMatrix = currPMatrix;
                // Pi = P(i-1) - ((AT*A)^-1 * AT * Y) *  (A * P(i-1) - Y)
                var AT = aMatrix.Transpose();
                var aInverse = Matrix.Multiply(AT, aMatrix).GetInverse();
                var dX = Matrix.Subtract(Matrix.Multiply(aMatrix, prevPMatrix), yMatrix);
                var pMatrix = Matrix.Subtract(prevPMatrix, Matrix.Multiply(Matrix.Multiply(aInverse, AT), dX));
                currPMatrix = (DenseMatrix<double>) pMatrix;
            }

            return currPMatrix;
        }

        private bool IsSubtractMatrixValuesLessWhenDelta(Matrix<double> first, Matrix<double> second,
            double delta)
        {
            var subtract = Matrix.Subtract(first, second);
            for (int i = 0; i < second.RowCount; i++)
                if (subtract[i, 0] < delta)
                    return false;
            return true;
        }

        private Matrix<double> GetVMatrix(Matrix<double> aMatrix, Matrix<double> yMatrix,
            Matrix<double> vecParamsMatrix)
        {
            var vMatrix = Matrix.Create<double>(SourceSystemCoordinates.Count * 3, 1);
            var Ap = Matrix.Multiply(aMatrix, vecParamsMatrix);
            // V = A * P - Y
            vMatrix = (DenseMatrix<double>) Matrix.Subtract(Ap, yMatrix);

            return vMatrix;
        }

        private double CalculateFCoefficient(Matrix<double> vMatrix)
        {
            return Matrix.Multiply(vMatrix.Transpose(), vMatrix)[0, 0];
        }

        private double CalculateMCoefficient(double fCoefficient)
        {
            return Math.Sqrt(fCoefficient / (SourceSystemCoordinates.Count * 3 - 7));
        }

        private Matrix<double> GetQMatrix(Matrix<double> aMatrix)
        {
            return Matrix.Multiply(aMatrix.Transpose(), aMatrix).GetInverse();
        }

        private Matrix<double> GetMeanSquareErrorsMatrix(Matrix<double> qMatrix, double mCoefficient)
        {
            var qDiagonal = qMatrix.GetDiagonal().ToColumnMatrix().SqrtInPlace();
            return Matrix.Multiply(qDiagonal, mCoefficient);
        }

        #region InternalMethodsForTesting

        internal Matrix<double> FormingCoordinateMatrixTst(List<Coordinates> list)
        {
            return FormingCoordinateMatrix(list);
        }

        internal Matrix<double> FormingAMatrixTst()
        {
            return FormingAMatrix();
        }

        internal Matrix<double> FormingYMatrixTst()
        {
            return FormingYMatrix();
        }

        internal Matrix<double> GetVectorWithTransformParametersTst(Matrix<double> aMatrix,
            Matrix<double> yMatrix)
        {
            return GetMatrixWithTransformParameters(aMatrix, yMatrix);
        }

        internal Matrix<double> GetVMatrixTst(Matrix<double> aMatrix, Matrix<double> yMatrix,
            Matrix<double> vecParamsMatrix)
        {
            return GetVMatrix(aMatrix, yMatrix, vecParamsMatrix);
        }

        internal double CalculateFCoefficientTst(Matrix<double> paramsTransformMatrix)
        {
            return CalculateFCoefficient(paramsTransformMatrix);
        }


        internal double CalculateMCoefficientTst(double fCoefficient)
        {
            return CalculateMCoefficient(fCoefficient);
        }

        internal Matrix<double> GetQMatrixTst(Matrix<double> aMatrix)
        {
            return GetQMatrix(aMatrix);
        }

        internal Matrix<double> GetMeanSquareErrorsMatrixTst(Matrix<double> qMatrix, double mCoefficient)
        {
            return GetMeanSquareErrorsMatrix(qMatrix, mCoefficient);
        }

        #endregion
    }
}