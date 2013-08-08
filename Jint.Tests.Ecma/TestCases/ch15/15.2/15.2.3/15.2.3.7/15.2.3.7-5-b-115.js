/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-115.js
 * @description Object.defineProperties - 'value' property of 'descObj' is inherited data property (8.10.5 step 5.a)
 */


function testcase() {
        var obj = {};

        var proto = {
            value: "inheritedDataProperty"
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
