// Copyright 2009 the Sputnik authors.  All rights reserved.
/**
 * The Number constructor has the property "POSITIVE_INFINITY"
 *
 * @path ch15/15.7/15.7.3/S15.7.3_A6.js
 * @description Checking existence of the property "POSITIVE_INFINITY"
 */

if(!Number.hasOwnProperty("POSITIVE_INFINITY")){
  $ERROR('#1: The Number constructor has the property "POSITIVE_INFINITY"');
}


