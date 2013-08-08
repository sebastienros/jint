/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-193.js
 * @description Object.defineProperties - 'get' property of 'descObj' is own data property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var getter = function () {
            return "ownDataProperty";
        };

        Object.defineProperties(obj, {
            property: {
                get: getter
            }
        });

        return obj.property === "ownDataProperty";
    }
runTestCase(testcase);
