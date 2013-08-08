/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-2-2.js
 * @description Object.create - returned object is an instance of Object
 */


function testcase() {

        var newObj = Object.create({});
        return newObj instanceof Object;
    }
runTestCase(testcase);
