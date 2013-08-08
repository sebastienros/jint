/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-159.js
 * @description Object.create - 'value' property of one property in 'Properties' is an inherited accessor property (8.10.5 step 5.a)
 */


function testcase() {

        var proto = {};

        Object.defineProperty(proto, "value", {
            get: function () {
                return "inheritedAccessorProperty";
            }
        });

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;

        var descObj = new ConstructFun();

        var newObj = Object.create({}, {
            prop: descObj
        });

        return newObj.prop === "inheritedAccessorProperty";
    }
runTestCase(testcase);
