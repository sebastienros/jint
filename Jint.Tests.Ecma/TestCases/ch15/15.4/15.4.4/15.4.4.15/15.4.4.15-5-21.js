/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-5-21.js
 * @description Array.prototype.lastIndexOf - value of 'fromIndex' which is an Object, and has an own toString method
 */


function testcase() {

        // objects inherit the default valueOf() method from Object
        // that simply returns itself. Since the default valueOf() method
        // does not return a primitive value, ES next tries to convert the object
        // to a number by calling its toString() method and converting the
        // resulting string to a number.
        var fromIndex = {
            toString: function () {
                return '2';
            }
        };
        var targetObj = new RegExp();

        return [0, true, targetObj, 3, false].lastIndexOf(targetObj, fromIndex) === 2 &&
        [0, true, 3, targetObj, false].lastIndexOf(targetObj, fromIndex) === -1;
    }
runTestCase(testcase);
