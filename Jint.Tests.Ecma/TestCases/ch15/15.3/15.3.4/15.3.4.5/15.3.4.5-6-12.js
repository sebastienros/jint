/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-6-12.js
 * @description Function.prototype.bind - F cannot get property which doesn't exist
 */


function testcase() {

        var foo = function () { };

        var obj = foo.bind({});
        return typeof (obj.property) === "undefined";
    }
runTestCase(testcase);
