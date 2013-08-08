/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.9/15.9.5/15.9.5.43/15.9.5.43-0-13.js
 * @description Date.prototype.toISOString - RangeError is thrown when value of date is Date(1970, 0, 100000001, 0, 0, 0, 1), the time zone is UTC(0)
 */


function testcase() {
        var timeZoneMinutes = new Date().getTimezoneOffset() * (-1);
        var date, dateStr;
        try {
            if (timeZoneMinutes > 0) {
                date = new Date(1970, 0, 100000001, 0, 0 + timeZoneMinutes + 60, 0, 1);
                dateStr = date.toISOString();
                return false;
            } else {
                date = new Date(1970, 0, 100000001, 0, 0, 0, 1);
                dateStr = date.toISOString();
                return false;
            }
        } catch (e) {
            return e instanceof RangeError;
        }
    }
runTestCase(testcase);
