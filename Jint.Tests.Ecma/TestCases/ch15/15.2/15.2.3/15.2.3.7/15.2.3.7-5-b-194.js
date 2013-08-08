/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-194.js
 * @description Object.defineProperties - 'get' property of 'descObj' is inherited data property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var getter = function () {
            return "inheritedDataProperty";
        };

        var proto = {
            get: getter
        };

        var Con = function () { };
        Con.prototype = proto;

        var descObj = new Con();

        Object.defineProperties(obj, {
            property: descObj
        });

        return obj.property === "inheritedDataProperty";
    }
runTestCase(testcase);
