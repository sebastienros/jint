// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The Function constructor has the property "prototype"
 *
 * @path ch15/15.3/15.3.3/S15.3.3_A1.js
 * @description Checking existence of the property "prototype"
 */

if(!Function.hasOwnProperty("prototype")){
  $ERROR('#1: The Function constructor has the property "prototype"');
}


