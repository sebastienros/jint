/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-0-11.js
 * @description with introduces scope - name lookup finds inner variable
 */


function testcase() {
  function f(o) {

    function innerf(o) {
      var x = 42;

      with (o) {
        return x;
      }
    }

    return innerf(o);
  }
  
  if (f({}) === 42) {
    return true;
  }
 }
runTestCase(testcase);
