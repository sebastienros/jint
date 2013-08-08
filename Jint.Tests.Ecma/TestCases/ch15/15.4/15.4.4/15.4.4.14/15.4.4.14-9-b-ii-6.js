/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-9-b-ii-6.js
 * @description Array.prototype.indexOf - array element is +0 and search element is -0
 */


function testcase() {

        return [+0].indexOf(-0) === 0;
    }
runTestCase(testcase);
