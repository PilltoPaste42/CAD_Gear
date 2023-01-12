namespace CGPlugin.Services;

using System;
using System.Collections;
using System.Collections.Generic;

using CGPlugin.Models;
using CGPlugin.Services.Interfaces;

using Inventor;

public class CitroenGearInventorBuilder : ICADGearBuilder
{
    private Application App { get; set; }

    private PartDocument Doc { get; set; }
    
    private TransientGeometry Geometry { get; set; }

    private PartComponentDefinition PartDefinition { get; set; }

    public CitroenGearModel Gear { get; set; }

    public void CreateDocument()
    {
        App = InventorWrapper.Connect();
        App.Visible = true;

        Doc = (PartDocument)App.Documents.Add(DocumentTypeEnum.kPartDocumentObject,
            App.FileManager.GetTemplateFile
            (DocumentTypeEnum.kPartDocumentObject,
                SystemOfMeasureEnum.kMetricSystemOfMeasure));
        
        Doc.UnitsOfMeasure.LengthUnits = UnitsTypeEnum.kMillimeterLengthUnits;

        Geometry = App.TransientGeometry;
        PartDefinition = Doc.ComponentDefinition;
    }

    /// <summary>
    /// Создает новый эскиз на рабочей плоскости.
    /// </summary>
    /// <param name="n">1 - ZY; 2 - ZX; 3 - XY.</param>
    /// <param name="offset">Расстояние от поверхности.</param>
    /// <returns>Новый эскиз.</returns>
    private PlanarSketch CreateNewSketch(int n, double offset)
    {
        var mainPlane = PartDefinition.WorkPlanes[n];
        var offsetPlane = PartDefinition.WorkPlanes.AddByPlaneAndOffset(
            mainPlane, offset);
        
        offsetPlane.Visible = false;
        
        var sketch = PartDefinition.Sketches.Add(offsetPlane);

        return sketch;
    }

    public void CreateExtra()
    {
        var camera = App.ActiveView.Camera;
        //camera.ViewOrientationType = ViewOrientationTypeEnum.kIsoTopRightViewOrientation;
        
        camera.Fit();
        camera.Apply();
    }

    public void CreateGearBody()
    {
        throw new NotImplementedException();
    }

    public void CreateTeeth()
    {
        throw new NotImplementedException();
    }

    private SketchPoint CreatePoint(PlanarSketch sketch, double x = 0, double y = 0)
    {
        return sketch.SketchPoints.Add(Geometry.CreatePoint2d(x, y));
    }

    private SketchLine CreateLine(PlanarSketch sketch, object begin, object end)
    {
        return sketch.SketchLines.AddByTwoPoints(begin, end);
    }

    private SketchCircle CreateCircle(PlanarSketch sketch, SketchPoint center, double radius)
    {
        return sketch.SketchCircles.AddByCenterRadius(center, radius);
    }

    private void DeleteAllSketchObjects(IList objects)
    {
        foreach (SketchEntity o in objects)
        {
            o.Delete();
        }
        objects.Clear();   
    }

    private Dictionary<string, SketchCircle> CreateMainCircles(PlanarSketch sketch, Dictionary<string,double> radii)
    {
        var circles = new Dictionary<string, SketchCircle>();
        var centerPoint = sketch.AddByProjectingEntity(PartDefinition.WorkPoints[1]) as SketchPoint;
        var textPoint = Geometry.CreatePoint2d();

        foreach (var radius in radii)
        {
            var circle = CreateCircle(sketch, centerPoint, radius.Value);
            circle.CenterSketchPoint.Merge(centerPoint);
            sketch.DimensionConstraints
                .AddDiameter((SketchEntity) circle, textPoint)
                    .Visible = false;

            circles.Add(radius.Key, circle);
        }

        return circles;
    }

    public void CreateTeethProfile()
    {
        const double engagementAngle = 20.0;

        var mainRadii = new Dictionary<string, double>()
        {
            {"pitch", (double)Gear.Diameter / 2 / 10},
            {"outside", (double)(Gear.Module * (Gear.TeethCount + 2)) / 2 / 10},
            {"root", (Gear.Module * (Gear.TeethCount - 2.5)) / 2 / 10}
        };

        var sketch = CreateNewSketch(3, 0);
        
        var points = sketch.SketchPoints;
        var circles = sketch.SketchCircles;
        var lines = sketch.SketchLines;
        
        var dCon = sketch.DimensionConstraints;
        var gCon = sketch.GeometricConstraints;

        var projectionPoint = sketch.AddByProjectingEntity(PartDefinition.WorkPoints[1]) as SketchPoint;

        var mainCircles = CreateMainCircles(sketch, mainRadii);

        mainCircles["pitch"].Construction = true;
        mainCircles["root"].Construction = true;

        var construct1 = CreateLine(sketch, projectionPoint, Geometry.CreatePoint2d(1,1));
        construct1.Construction = true;
        gCon.AddCoincident((SketchEntity)mainCircles["outside"], (SketchEntity)construct1.EndSketchPoint);
        gCon.AddVertical((SketchEntity)construct1);


        // Построение baseCircle

        var tempPoints = new List<SketchPoint>
        {
            CreatePoint(sketch),
            CreatePoint(sketch, 1, mainRadii["pitch"]),
            CreatePoint(sketch, 1, 1)
        };
        
        var tempLines = new List<SketchLine>
        {
            CreateLine(sketch, tempPoints[0], tempPoints[2]),
            CreateLine(sketch, tempPoints[0], tempPoints[1])
        };

        gCon.AddCoincident((SketchEntity)tempPoints[0], (SketchEntity)mainCircles["pitch"]);
        gCon.AddCoincident((SketchEntity)tempPoints[0], (SketchEntity)construct1);

        gCon.AddHorizontalAlign(tempPoints[0], tempPoints[1]);

        gCon.AddCoincident((SketchEntity)tempPoints[2], (SketchEntity)mainCircles["pitch"]);

        var angle = dCon.AddTwoLineAngle(tempLines[1], tempLines[0], projectionPoint.Geometry);
        angle.Parameter.Value = Math.PI/180 * (180 - engagementAngle);
        angle.Visible = false;

        var baseCircle = CreateCircle(sketch, projectionPoint, 1);
        baseCircle.CenterSketchPoint.Merge(projectionPoint);
        gCon.AddTangent((SketchEntity)baseCircle, (SketchEntity)tempLines[0]);

        DeleteAllSketchObjects(tempLines);
        DeleteAllSketchObjects(tempPoints);

        dCon.AddDiameter((SketchEntity)baseCircle, projectionPoint.Geometry).Visible = false;
        baseCircle.Construction = true;

        mainCircles.Add("base", baseCircle);
        mainRadii.Add("base", baseCircle.Radius);

        // Построение эвольвенты

        tempPoints.AddRange(new[]
        {
            CreatePoint(sketch, 0, mainRadii["pitch"]),
            CreatePoint(sketch, -mainRadii["pitch"], mainRadii["pitch"]),
            CreatePoint(sketch, mainRadii["base"], mainRadii["base"]),
            CreatePoint(sketch, 0, mainRadii["root"]),
            CreatePoint(sketch, 0, mainRadii["root"])
        });

        var s = Math.PI * Gear.Module / 2 / 10;
        var r = mainRadii["pitch"] / 3;

        var tempCircles = new List<SketchCircle>
        {
            CreateCircle(sketch, tempPoints[0], s),
            CreateCircle(sketch, tempPoints[1], r),
            CreateCircle(sketch, tempPoints[0], 0.75 * Gear.Module * Math.PI/ 1 / 10)
        };

        foreach (SketchEntity sketchCircle in tempCircles)
        {
            dCon.AddRadius(sketchCircle, projectionPoint.Geometry).Visible = false;
        }

        gCon.AddCoincident((SketchEntity)tempCircles[0].CenterSketchPoint, (SketchEntity)mainCircles["pitch"]);
        gCon.AddCoincident((SketchEntity)tempCircles[0].CenterSketchPoint, (SketchEntity)construct1);

        gCon.AddCoincident((SketchEntity)tempCircles[1].CenterSketchPoint, (SketchEntity)mainCircles["pitch"]);
        gCon.AddCoincident((SketchEntity)tempCircles[1].CenterSketchPoint, (SketchEntity)tempCircles[0]);

        var startAngle1 = Math.PI / 180 * 180;
        var sweepAngle1 = Math.PI / 180 * 50;
        var involuteArk1 =
            sketch.SketchArcs
                .AddByCenterStartSweepAngle(tempPoints[2], r, startAngle1, sweepAngle1);

        dCon.AddDiameter((SketchEntity) involuteArk1, projectionPoint.Geometry).Visible = false;
        gCon.AddCoincident((SketchEntity) involuteArk1.CenterSketchPoint, (SketchEntity) mainCircles["base"]);
        gCon.AddCoincident((SketchEntity) involuteArk1.CenterSketchPoint, (SketchEntity) tempCircles[1]);

        gCon.AddCoincident((SketchEntity) involuteArk1.StartSketchPoint, (SketchEntity) mainCircles["outside"]);
        gCon.AddCoincident((SketchEntity)involuteArk1.EndSketchPoint, (SketchEntity)mainCircles["base"]);

        var startAngle2 = Math.PI / 180 * 0;
        var sweepAngle2 = Math.PI / 180 * -90;
        var involuteArk2Rad = 0.2 * Gear.Module / 10;
        var involuteArk2 = sketch.SketchArcs.AddByCenterStartSweepAngle(
            CreatePoint(sketch, 0, mainRadii["root"]), 
            involuteArk2Rad,
            startAngle2, 
            sweepAngle2);
        dCon.AddRadius((SketchEntity)involuteArk2, projectionPoint.Geometry).Visible = false;
        dCon.AddArcLength((SketchEntity)involuteArk2, projectionPoint.Geometry).Visible = false;

        var involuteLine1 = CreateLine(sketch, involuteArk1.EndSketchPoint, involuteArk2.EndSketchPoint);

        gCon.AddCoincident((SketchEntity) involuteArk2.StartSketchPoint, (SketchEntity) mainCircles["root"]);
        gCon.AddPerpendicular((SketchEntity) involuteLine1, (SketchEntity) mainCircles["root"]);

        gCon.AddCoincident((SketchEntity) tempCircles[2].CenterSketchPoint,
            (SketchEntity) tempCircles[0].CenterSketchPoint);
        gCon.AddCoincident((SketchEntity) tempPoints[1], (SketchEntity) mainCircles["pitch"]);
        gCon.AddCoincident((SketchEntity) tempPoints[1], (SketchEntity) tempCircles[2]);

        gCon.AddCoincident((SketchEntity)tempPoints[4], (SketchEntity)mainCircles["root"]);
        tempLines.Add(CreateLine(sketch, tempPoints[1], tempPoints[4]));
        gCon.AddPerpendicular((SketchEntity) tempLines[0], (SketchEntity) mainCircles["root"]);

        var point1 = involuteArk2.StartSketchPoint;
        var point2 = tempPoints[4].Geometry;
        var involuteArk3 = 
            sketch.SketchArcs.AddByCenterStartEndPoint(
                projectionPoint,
                point1,
                CreatePoint(sketch, point2.X, point2.Y)
                );

        gCon.AddConcentric((SketchEntity) involuteArk3, (SketchEntity) mainCircles["root"]);
        gCon.AddTangent((SketchEntity)involuteArk2, (SketchEntity)involuteArk3);

        DeleteAllSketchObjects(tempCircles);
        DeleteAllSketchObjects(tempLines);
        DeleteAllSketchObjects(tempPoints);

        dCon.AddArcLength((SketchEntity) involuteArk3, projectionPoint.Geometry).Visible = false;
        gCon.AddCoincident((SketchEntity) involuteArk3.EndSketchPoint, (SketchEntity) construct1);

        var involuteArk4 = sketch.SketchArcs.AddByCenterStartEndPoint(projectionPoint, involuteArk3.EndSketchPoint,
            Geometry.CreatePoint2d(-1, mainRadii["root"]));
        gCon.AddSymmetry((SketchEntity) involuteArk4, (SketchEntity) involuteArk3, construct1);

        var involuteArk5 = sketch.SketchArcs.AddByCenterStartEndPoint(
            Geometry.CreatePoint2d(-1, mainRadii["base"]), 
            involuteArk4.EndSketchPoint,
             Geometry.CreatePoint2d(-1, mainRadii["pitch"]),
            false);
        gCon.AddSymmetry((SketchEntity)involuteArk5, (SketchEntity)involuteArk2, construct1);

        var involuteLine2 = sketch.SketchLines.AddByTwoPoints(
            involuteArk5.StartSketchPoint,
            Geometry.CreatePoint2d(-1, mainRadii["base"])
            );
        gCon.AddSymmetry((SketchEntity)involuteLine2, (SketchEntity)involuteLine1, construct1);
        gCon.AddCoincident((SketchEntity) involuteLine2.EndSketchPoint, (SketchEntity) mainCircles["base"]);

        var involuteArk6 = sketch.SketchArcs.AddByCenterStartEndPoint(
            Geometry.CreatePoint2d(-1, mainRadii["pitch"]),
            involuteLine2.EndSketchPoint,
            Geometry.CreatePoint2d(0, mainRadii["outside"])
            );
        gCon.AddCoincident((SketchEntity)involuteArk6.EndSketchPoint, (SketchEntity)mainCircles["outside"]);
        gCon.AddSymmetry((SketchEntity)involuteArk6, (SketchEntity)involuteArk1, construct1);
        
        sketch.Solve();
    }
}