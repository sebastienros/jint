/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-199.js
 * @description Object.defineProperties - 'get' property of 'descObj' is own accessor property that overrides an inherited data property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var proto = {
            get: function () {
                return "inheritedDataProperty";
            }
        };

        var Con = function () { };
        Con.prototype = proto;

        var descObj = new Con();

        Object.defineProperty(descObj, "get", {
            get: function () {
                return function () {
                    return "ownAccessorProperty";
                };
            }
        });

        Object.defineProperties(obj, {
            property: descObj
        });

        return obj.property === "ownAccessorProperty";
    }
runTestCase(testcase);
