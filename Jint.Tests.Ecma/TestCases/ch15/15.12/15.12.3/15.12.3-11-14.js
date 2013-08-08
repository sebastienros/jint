/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.12/15.12.3/15.12.3-11-14.js
 * @description Applying JSON.stringify to a  function returns undefined.
 */


function testcase() {
  return JSON.stringify(function() {}) === undefined;
  }
runTestCase(testcase);
