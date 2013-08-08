/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-9-c-i-4.js
 * @description Array.prototype.filter - element to be retrieved is own data property that overrides an inherited data property on an Array
 */


function testcase() {

        function callbackfn(val, idx, obj) {
            return (idx === 0) && (val === 12);
        }

        try {
            Array.prototype[0] = 11;
            var newArr = [12].filter(callbackfn);

            return newArr.length === 1 && newArr[0] === 12;
        } finally {
            delete Array.prototype[0];
        }
    }
runTestCase(testcase);
