/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-5-b-223.js
 * @description Object.defineProperties - value of 'get' property of 'descObj' is a function (8.10.5 step 7.b)
 */


function testcase() {
        var obj = {};

        var getter = function () {
            return 100;
        };

        Object.defineProperties(obj, {
            property: {
                get: getter
            }
        });

        return obj.property === 100;
    }
runTestCase(testcase);
