/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-207.js
 * @description Object.defineProperties - 'descObj' is a Boolean object which implements its own [[Get]] method to get 'get' property (8.10.5 step 7.a)
 */


function testcase() {
        var obj = {};

        var descObj = new Boolean(false);

        descObj.get = function () {
            return "Boolean";
        };

        Object.defineProperties(obj, {
            property: descObj
        });

        return obj.property === "Boolean";
    }
runTestCase(testcase);
