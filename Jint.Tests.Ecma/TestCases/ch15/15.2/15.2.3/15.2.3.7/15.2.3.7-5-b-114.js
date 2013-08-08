/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-114.js
 * @description Object.defineProperties - 'value' property of 'descObj' is own data property (8.10.5 step 5.a)
 */


function testcase() {
        var obj = {};

        Object.defineProperties(obj, {
            property: {
                value: "ownDataProperty"
            }
        });

        return obj.property === "ownDataProperty";
    }
runTestCase(testcase);
