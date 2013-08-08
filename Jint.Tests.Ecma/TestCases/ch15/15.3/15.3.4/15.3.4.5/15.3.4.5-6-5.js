/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-5.js
 * @description Function.prototype.bind - F can get own accessor property
 */


function testcase() {

        var foo = function () { };

        var obj = foo.bind({});
        Object.defineProperty(obj, "property", {
            get: function () {
                return 12;
            }
        });
        return obj.property === 12;
    }
runTestCase(testcase);
