/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-3.js
 * @description Array.prototype.indexOf - both type of array element and type of search element are null
 */


function testcase() {

        return [null].indexOf(null) === 0;
    }
runTestCase(testcase);
