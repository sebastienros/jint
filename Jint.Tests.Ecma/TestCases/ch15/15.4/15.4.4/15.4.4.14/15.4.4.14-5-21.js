/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-21.js
 * @description Array.prototype.indexOf - value of 'fromIndex' is an Object, which has an own toString method
 */


function testcase() {

        // objects inherit the default valueOf() method from Object
        // that simply returns itself. Since the default valueOf() method
        // does not return a primitive value, ES next tries to convert the object
        // to a number by calling its toString() method and converting the
        // resulting string to a number.
        var fromIndex = {
            toString: function () {
                return '1';
            }
        };

        return [0, true].indexOf(true, fromIndex) === 1;
    }
runTestCase(testcase);
