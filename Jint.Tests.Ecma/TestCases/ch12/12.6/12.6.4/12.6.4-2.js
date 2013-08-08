/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.6/12.6.4/12.6.4-2.js
 * @description The for-in Statement - the values of [[Enumerable]] attributes are not considered when determining if a property of a prototype object is shadowed by a previous object on the prototype chain
 */


function testcase() {
        var obj = {};

        var proto = {};

        Object.defineProperty(proto, "prop", {
            value: "inheritedValue",
            enumerable: false,
            configurable: true,
            writable: true
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var child = new ConstructFun();

        Object.defineProperty(child, "prop1", {
            value: "overridedValue1",
            enumerable: false
        });
        Object.defineProperty(child, "prop2", {
            value: "overridedValue2",
            enumerable: true
        });
        var accessedProp1 = false;
        var accessedProp2 = false;

        for (var p in child) {
            if (child.hasOwnProperty(p)) {
                if (p === "prop1") {
                    accessedProp1 = true;
                }
                if (p === "prop2") {
                    accessedProp2 = true;
                }
            }
        }
        return !accessedProp1 && accessedProp2 && child.prop1 === "overridedValue1" && child.prop2 === "overridedValue2";
    }
runTestCase(testcase);
