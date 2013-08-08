/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-33.js
 * @description Array.prototype.indexOf match on the first element, a middle element and the last element when 'fromIndex' is passed
 */


function testcase() {

        return [0, 1, 2, 3, 4].indexOf(0, 0) === 0 &&
            [0, 1, 2, 3, 4].indexOf(2, 1) === 2 &&
            [0, 1, 2, 3, 4].indexOf(2, 2) === 2 &&
            [0, 1, 2, 3, 4].indexOf(4, 2) === 4 &&
            [0, 1, 2, 3, 4].indexOf(4, 4) === 4;
    }
runTestCase(testcase);
